using System;
using System.Collections.Generic;
using TileLayout.Core;
using TileLayout.Core.Models;

namespace TileLayout.AutoCAD.Adapter
{
    public enum DoorBlockRecognitionRoute
    {
        Dynamic,
        FrozenStaticSignature
    }

    public enum DoorObjectRecognitionStatus
    {
        High,
        Ambiguous,
        Unsupported,
        Invalid
    }

    public enum DoorObjectRecognitionRejectionCode
    {
        None,
        UnsupportedObjectType,
        NotTopLevelModelSpace,
        StaticBlock,
        ExternalReference,
        NonUniformScaling,
        ProxyObject,
        UnreadableNestedGeometry,
        CyclicNestedGeometry,
        NonPlanarGeometry,
        NonFiniteGeometry,
        NoCompleteSingleSwingSignature,
        MultipleDistinctSignatures,
        PointAdapterRejected
    }

    public sealed class DoorObjectRecognitionResult
    {
        private DoorObjectRecognitionResult(
            DoorObjectRecognitionStatus status,
            DoorObjectRecognitionRejectionCode rejectionCode,
            string reason,
            DoorOpeningProjectionResult projection,
            int distinctCandidateCount,
            DoorBlockRecognitionRoute? route)
        {
            Status = status;
            RejectionCode = rejectionCode;
            Reason = reason ?? string.Empty;
            Projection = projection;
            DistinctCandidateCount = distinctCandidateCount;
            Route = route;
        }

        public DoorObjectRecognitionStatus Status { get; }

        public DoorObjectRecognitionRejectionCode RejectionCode { get; }

        public string Reason { get; }

        public DoorOpeningProjectionResult Projection { get; }

        public int DistinctCandidateCount { get; }

        public DoorBlockRecognitionRoute? Route { get; }

        public bool IsHigh => Status == DoorObjectRecognitionStatus.High;

        public static DoorObjectRecognitionResult Rejected(
            DoorObjectRecognitionStatus status,
            DoorObjectRecognitionRejectionCode rejectionCode,
            string reason)
        {
            if (status == DoorObjectRecognitionStatus.High)
            {
                throw new ArgumentOutOfRangeException(nameof(status));
            }

            return new DoorObjectRecognitionResult(
                status,
                rejectionCode,
                reason,
                null,
                0,
                null);
        }

        internal static DoorObjectRecognitionResult FromGeometry(
            DoorGeometryRecognitionResult geometry,
            DoorOpeningProjectionResult projection,
            DoorBlockRecognitionRoute route)
        {
            if (geometry.IsHigh && projection.IsValid)
            {
                return new DoorObjectRecognitionResult(
                    DoorObjectRecognitionStatus.High,
                    DoorObjectRecognitionRejectionCode.None,
                    string.Empty,
                    projection,
                    1,
                    route);
            }

            if (geometry.IsHigh)
            {
                return new DoorObjectRecognitionResult(
                    DoorObjectRecognitionStatus.Invalid,
                    DoorObjectRecognitionRejectionCode.PointAdapterRejected,
                    projection.ErrorMessage,
                    projection,
                    1,
                    route);
            }

            DoorObjectRecognitionStatus status = geometry.Status
                == DoorGeometryRecognitionStatus.Ambiguous
                ? DoorObjectRecognitionStatus.Ambiguous
                : DoorObjectRecognitionStatus.Unsupported;
            DoorObjectRecognitionRejectionCode rejectionCode;
            string reason;
            switch (geometry.RejectionCode)
            {
                case DoorGeometryRejectionCode.NonFiniteGeometry:
                    rejectionCode =
                        DoorObjectRecognitionRejectionCode.NonFiniteGeometry;
                    reason = "所选块包含非有限 LINE/ARC 坐标。";
                    break;
                case DoorGeometryRejectionCode.MultipleDistinctSignatures:
                    rejectionCode = DoorObjectRecognitionRejectionCode
                        .MultipleDistinctSignatures;
                    reason = string.Format(
                        "当前可见几何产生 {0} 个不同门洞候选，无法唯一确定单扇门。",
                        geometry.DistinctCandidateCount);
                    break;
                default:
                    rejectionCode = DoorObjectRecognitionRejectionCode
                        .NoCompleteSingleSwingSignature;
                    reason = "当前可见 LINE/ARC 中没有完整且唯一的单扇平开门线弧签名。";
                    break;
            }

            return new DoorObjectRecognitionResult(
                status,
                rejectionCode,
                reason,
                null,
                geometry.DistinctCandidateCount,
                route);
        }
    }

    public static class DoorObjectRecognitionCoordinator
    {
        public static DoorObjectRecognitionResult Recognize(
            AxisAlignedRectangle room,
            IEnumerable<LineSegment3D> lines,
            IEnumerable<ArcSegment3D> arcs)
        {
            return Recognize(
                room,
                lines,
                arcs,
                DoorBlockRecognitionRoute.Dynamic);
        }

        public static DoorObjectRecognitionResult Recognize(
            AxisAlignedRectangle room,
            IEnumerable<LineSegment3D> lines,
            IEnumerable<ArcSegment3D> arcs,
            DoorBlockRecognitionRoute route)
        {
            if (room == null)
            {
                throw new ArgumentNullException(nameof(room));
            }

            DoorGeometryRecognitionResult geometry;
            switch (route)
            {
                case DoorBlockRecognitionRoute.Dynamic:
                    geometry = DoorGeometryRecognizer.Recognize(lines, arcs);
                    break;
                case DoorBlockRecognitionRoute.FrozenStaticSignature:
                    geometry = StaticDoorGeometryRecognizer.Recognize(
                        lines,
                        arcs);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(route));
            }

            if (!geometry.IsHigh)
            {
                return DoorObjectRecognitionResult.FromGeometry(
                    geometry,
                    null,
                    route);
            }

            DoorOpeningProjectionResult projection =
                DoorOpeningPointAdapter.ProjectToRoomWall(
                    room,
                    geometry.FirstOpeningPoint,
                    geometry.SecondOpeningPoint);
            return DoorObjectRecognitionResult.FromGeometry(
                geometry,
                projection,
                route);
        }
    }
}
