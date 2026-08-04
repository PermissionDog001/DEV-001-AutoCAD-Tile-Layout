using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using TileLayout.Core.Models;
using CoreLineSegment3D = TileLayout.Core.Models.LineSegment3D;
using CorePoint3D = TileLayout.Core.Models.Point3D;

namespace TileLayout.AutoCAD.Adapter
{
    internal sealed class DoorBlockGeometryReadResult
    {
        private DoorBlockGeometryReadResult(
            IReadOnlyCollection<CoreLineSegment3D> lines,
            IReadOnlyCollection<ArcSegment3D> arcs,
            DoorObjectRecognitionResult rejection,
            DoorBlockRecognitionRoute? route)
        {
            Lines = lines;
            Arcs = arcs;
            Rejection = rejection;
            Route = route;
        }

        public bool IsSuccessful => Rejection == null;

        public IReadOnlyCollection<CoreLineSegment3D> Lines { get; }

        public IReadOnlyCollection<ArcSegment3D> Arcs { get; }

        public DoorObjectRecognitionResult Rejection { get; }

        public DoorBlockRecognitionRoute? Route { get; }

        public static DoorBlockGeometryReadResult Success(
            IReadOnlyCollection<CoreLineSegment3D> lines,
            IReadOnlyCollection<ArcSegment3D> arcs,
            DoorBlockRecognitionRoute route)
        {
            return new DoorBlockGeometryReadResult(
                lines,
                arcs,
                null,
                route);
        }

        public static DoorBlockGeometryReadResult Rejected(
            DoorObjectRecognitionRejectionCode code,
            string reason)
        {
            return new DoorBlockGeometryReadResult(
                null,
                null,
                DoorObjectRecognitionResult.Rejected(
                    DoorObjectRecognitionStatus.Unsupported,
                    code,
                    reason),
                null);
        }
    }

    internal static class DoorBlockGeometryReader
    {
        private const int MaximumNestedDepth = 32;

        public static DoorBlockGeometryReadResult Read(
            Database database,
            ObjectId selectedId)
        {
            if (database == null)
            {
                throw new ArgumentNullException(nameof(database));
            }

            try
            {
                return ReadCore(database, selectedId);
            }
            catch (Autodesk.AutoCAD.Runtime.Exception)
            {
                return UnreadableNestedGeometry();
            }
            catch (InvalidOperationException)
            {
                return UnreadableNestedGeometry();
            }
        }

        private static DoorBlockGeometryReadResult ReadCore(
            Database database,
            ObjectId selectedId)
        {
            using (Transaction transaction =
                database.TransactionManager.StartTransaction())
            {
                BlockTable blockTable = (BlockTable)transaction.GetObject(
                    database.BlockTableId,
                    OpenMode.ForRead);
                ObjectId modelSpaceId = blockTable[BlockTableRecord.ModelSpace];
                BlockReference blockReference = transaction.GetObject(
                    selectedId,
                    OpenMode.ForRead,
                    false) as BlockReference;
                if (blockReference == null)
                {
                    return FromPolicyRejection(
                        DoorBlockInputPolicy.ValidateForFrozenSignatureInspection(
                            false,
                            false,
                            false,
                            false,
                            1.0,
                            1.0,
                            1.0));
                }

                BlockTableRecord definition = transaction.GetObject(
                    blockReference.BlockTableRecord,
                    OpenMode.ForRead) as BlockTableRecord;
                if (definition == null)
                {
                    return UnreadableNestedGeometry();
                }

                Scale3d scale = blockReference.ScaleFactors;
                DoorObjectRecognitionResult policyRejection =
                    DoorBlockInputPolicy.ValidateForFrozenSignatureInspection(
                        true,
                        blockReference.OwnerId == modelSpaceId,
                        blockReference.IsDynamicBlock,
                        definition.IsFromExternalReference
                            || definition.IsFromOverlayReference,
                        scale.X,
                        scale.Y,
                        scale.Z);
                if (policyRejection != null)
                {
                    return FromPolicyRejection(policyRejection);
                }

                var lines = new List<CoreLineSegment3D>();
                var arcs = new List<ArcSegment3D>();
                var path = new HashSet<ObjectId>();
                path.Add(blockReference.BlockTableRecord);
                DoorBlockGeometryReadResult failure = ReadExploded(
                    transaction,
                    blockReference,
                    0,
                    path,
                    lines,
                    arcs);
                if (failure != null)
                {
                    return failure;
                }

                DoorBlockRecognitionRoute route = blockReference.IsDynamicBlock
                    ? DoorBlockRecognitionRoute.Dynamic
                    : DoorBlockRecognitionRoute.FrozenStaticSignature;
                return DoorBlockGeometryReadResult.Success(
                    lines,
                    arcs,
                    route);
            }
        }

        private static DoorBlockGeometryReadResult ReadExploded(
            Transaction transaction,
            Entity entity,
            int depth,
            ISet<ObjectId> path,
            ICollection<CoreLineSegment3D> lines,
            ICollection<ArcSegment3D> arcs)
        {
            if (depth > MaximumNestedDepth)
            {
                return DoorBlockGeometryReadResult.Rejected(
                    DoorObjectRecognitionRejectionCode.CyclicNestedGeometry,
                    "所选块嵌套过深或存在循环引用，无法安全读取当前可见几何。");
            }

            var exploded = new DBObjectCollection();
            try
            {
                entity.Explode(exploded);
            }
            catch (Autodesk.AutoCAD.Runtime.Exception)
            {
                return UnreadableNestedGeometry();
            }

            DoorBlockGeometryReadResult firstFailure = null;
            foreach (DBObject item in exploded)
            {
                try
                {
                    if (firstFailure == null)
                    {
                        firstFailure = ReadEntity(
                            transaction,
                            item as Entity,
                            depth + 1,
                            path,
                            lines,
                            arcs);
                    }
                }
                finally
                {
                    item.Dispose();
                }
            }

            return firstFailure;
        }

        private static DoorBlockGeometryReadResult ReadEntity(
            Transaction transaction,
            Entity entity,
            int depth,
            ISet<ObjectId> path,
            ICollection<CoreLineSegment3D> lines,
            ICollection<ArcSegment3D> arcs)
        {
            if (entity == null)
            {
                return UnreadableNestedGeometry();
            }

            string runtimeName = entity.GetRXClass().Name;
            if (string.Equals(
                runtimeName,
                "AcDbProxyEntity",
                StringComparison.OrdinalIgnoreCase))
            {
                return DoorBlockGeometryReadResult.Rejected(
                    DoorObjectRecognitionRejectionCode.ProxyObject,
                    "所选块包含代理对象，不读取其门几何。");
            }

            Line line = entity as Line;
            if (line != null)
            {
                lines.Add(
                    new CoreLineSegment3D(
                        ToCorePoint(line.StartPoint),
                        ToCorePoint(line.EndPoint)));
                return null;
            }

            Arc arc = entity as Arc;
            if (arc != null)
            {
                Vector3d normal = arc.Normal;
                if (Math.Abs(normal.X) > TileLayout.Core.GeometryTolerance.Coordinate
                    || Math.Abs(normal.Y)
                        > TileLayout.Core.GeometryTolerance.Coordinate
                    || Math.Abs(Math.Abs(normal.Z) - 1.0)
                        > TileLayout.Core.GeometryTolerance.Coordinate)
                {
                    return DoorBlockGeometryReadResult.Rejected(
                        DoorObjectRecognitionRejectionCode.NonPlanarGeometry,
                        "所选块包含非 WCS XY 平面的开启弧，三维门不支持。");
                }

                arcs.Add(
                    new ArcSegment3D(
                        ToCorePoint(arc.Center),
                        ToCorePoint(arc.StartPoint),
                        ToCorePoint(arc.EndPoint),
                        arc.Radius));
                return null;
            }

            if (entity is Ellipse)
            {
                return NonUniformScaling();
            }

            BlockReference nested = entity as BlockReference;
            if (nested == null)
            {
                return null;
            }

            if (!HasUniformNonZeroScale(nested.ScaleFactors))
            {
                return NonUniformScaling();
            }

            ObjectId definitionId = nested.BlockTableRecord;
            if (definitionId.IsNull || !definitionId.IsValid)
            {
                return UnreadableNestedGeometry();
            }

            BlockTableRecord definition = transaction.GetObject(
                definitionId,
                OpenMode.ForRead,
                false) as BlockTableRecord;
            if (definition == null)
            {
                return UnreadableNestedGeometry();
            }

            if (definition.IsFromExternalReference
                || definition.IsFromOverlayReference)
            {
                return DoorBlockGeometryReadResult.Rejected(
                    DoorObjectRecognitionRejectionCode.ExternalReference,
                    "所选块包含外部参照嵌套，不读取其门几何。");
            }

            if (!path.Add(definitionId))
            {
                return DoorBlockGeometryReadResult.Rejected(
                    DoorObjectRecognitionRejectionCode.CyclicNestedGeometry,
                    "所选块存在循环嵌套，无法安全读取当前可见几何。");
            }

            try
            {
                return ReadExploded(
                    transaction,
                    nested,
                    depth,
                    path,
                    lines,
                    arcs);
            }
            finally
            {
                path.Remove(definitionId);
            }
        }

        private static bool HasUniformNonZeroScale(Scale3d scale)
        {
            return DoorBlockInputPolicy.HasUniformNonZeroScale(
                scale.X,
                scale.Y,
                scale.Z);
        }

        private static CorePoint3D ToCorePoint(Point3d point)
        {
            return new CorePoint3D(point.X, point.Y, point.Z);
        }

        private static DoorBlockGeometryReadResult NonUniformScaling()
        {
            return DoorBlockGeometryReadResult.Rejected(
                DoorObjectRecognitionRejectionCode.NonUniformScaling,
                "所选块或其嵌套使用非均匀缩放，圆弧门几何不支持。");
        }

        private static DoorBlockGeometryReadResult FromPolicyRejection(
            DoorObjectRecognitionResult rejection)
        {
            return DoorBlockGeometryReadResult.Rejected(
                rejection.RejectionCode,
                rejection.Reason);
        }

        private static DoorBlockGeometryReadResult
            UnreadableNestedGeometry()
        {
            return DoorBlockGeometryReadResult.Rejected(
                DoorObjectRecognitionRejectionCode.UnreadableNestedGeometry,
                "所选块的当前可见嵌套几何无法完整只读展开。");
        }
    }
}
