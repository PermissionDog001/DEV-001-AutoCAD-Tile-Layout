using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using TileLayout.Core;
using CorePoint3D = TileLayout.Core.Models.Point3D;

namespace TileLayout.AutoCAD.Adapter
{
    internal static class OrthogonalLayoutStartPointEntityFactory
    {
        private const short StartPointColorIndex = 3;
        private const string StartPointLabel = "起铺点";

        internal const int ExpectedEntityCount = 10;

        internal static IReadOnlyList<Entity> CreateTransient(
            Database database,
            LayoutDrawingPlan plan)
        {
            return Create(database, plan);
        }

        internal static IReadOnlyList<Entity> Create(
            Database database,
            LayoutDrawingPlan plan)
        {
            if (database == null)
            {
                throw new ArgumentNullException(nameof(database));
            }

            if (plan == null)
            {
                throw new ArgumentNullException(nameof(plan));
            }

            if (plan.StartPoint == null)
            {
                return new List<Entity>();
            }

            double tileScale = GetTileScale(plan);
            double armLength = Math.Max(
                25.0,
                Math.Min(
                    tileScale * 0.45,
                    Math.Min(plan.Width, plan.Height) * 0.20));
            double circleRadius = armLength * 0.18;
            double arrowHeadLength = armLength * 0.20;
            double arrowHeadHalfWidth = armLength * 0.10;
            double textHeight = Math.Max(18.0, armLength * 0.30);
            Vector3d inward = ToVector(plan.StartPoint.InwardDirection);
            Vector3d along = ToVector(plan.StartPoint.AlongWallDirection);
            Point3d center = ToAcadPoint(plan.StartPoint.Position);

            var entities = new List<Entity>();
            entities.Add(CreateCircle(database, center, circleRadius));
            entities.Add(CreateLine(
                database,
                center - Vector3d.XAxis * circleRadius,
                center + Vector3d.XAxis * circleRadius));
            entities.Add(CreateLine(
                database,
                center - Vector3d.YAxis * circleRadius,
                center + Vector3d.YAxis * circleRadius));
            entities.AddRange(CreateArrow(
                database,
                center,
                inward,
                armLength,
                arrowHeadLength,
                arrowHeadHalfWidth));
            entities.AddRange(CreateArrow(
                database,
                center,
                along,
                armLength,
                arrowHeadLength,
                arrowHeadHalfWidth));

            Point3d textPosition = center
                + inward * (armLength * 0.72)
                + along * (armLength * 0.58);
            var label = new DBText
            {
                Position = textPosition,
                Height = textHeight,
                TextString = StartPointLabel,
                Rotation = 0.0
            };
            label.SetDatabaseDefaults(database);
            label.Color = Color.FromColorIndex(
                ColorMethod.ByAci,
                StartPointColorIndex);
            entities.Add(label);
            return entities;
        }

        private static IReadOnlyList<Entity> CreateArrow(
            Database database,
            Point3d center,
            Vector3d direction,
            double armLength,
            double arrowHeadLength,
            double arrowHeadHalfWidth)
        {
            Vector3d perpendicular = Math.Abs(direction.X) > 0.5
                ? Vector3d.YAxis
                : Vector3d.XAxis;
            Point3d tip = center + direction * armLength;
            Point3d shaftStart = center
                + direction * (armLength * 0.18);
            // A short open arrow is represented by three LINE entities so
            // transient and formal geometry use exactly the same objects.
            return new List<Entity>
            {
                CreateLine(
                    database,
                    shaftStart,
                    tip - direction * arrowHeadLength),
                CreateLine(
                    database,
                    tip,
                    tip - direction * arrowHeadLength
                        + perpendicular * arrowHeadHalfWidth),
                CreateLine(
                    database,
                    tip,
                    tip - direction * arrowHeadLength
                        - perpendicular * arrowHeadHalfWidth)
            };
        }

        private static double GetTileScale(LayoutDrawingPlan plan)
        {
            double scale = 0.0;
            foreach (LayoutDrawingTile tile in plan.Tiles)
            {
                if (tile.IsFullTile)
                {
                    scale = Math.Max(scale, tile.NominalWidth);
                    scale = Math.Max(scale, tile.NominalHeight);
                }
            }

            return scale > GeometryTolerance.Coordinate
                ? scale
                : Math.Min(plan.Width, plan.Height);
        }

        private static Circle CreateCircle(
            Database database,
            Point3d center,
            double radius)
        {
            var circle = new Circle(center, Vector3d.ZAxis, radius);
            circle.SetDatabaseDefaults(database);
            circle.Color = Color.FromColorIndex(
                ColorMethod.ByAci,
                StartPointColorIndex);
            return circle;
        }

        private static Line CreateLine(
            Database database,
            Point3d start,
            Point3d end)
        {
            var line = new Line(start, end);
            line.SetDatabaseDefaults(database);
            line.Color = Color.FromColorIndex(
                ColorMethod.ByAci,
                StartPointColorIndex);
            return line;
        }

        private static Vector3d ToVector(RoomSide side)
        {
            switch (side)
            {
                case RoomSide.West:
                    return -Vector3d.XAxis;
                case RoomSide.East:
                    return Vector3d.XAxis;
                case RoomSide.South:
                    return -Vector3d.YAxis;
                case RoomSide.North:
                    return Vector3d.YAxis;
                default:
                    throw new ArgumentOutOfRangeException(nameof(side));
            }
        }

        private static Point3d ToAcadPoint(CorePoint3D point)
        {
            return new Point3d(point.X, point.Y, point.Z);
        }

    }
}
