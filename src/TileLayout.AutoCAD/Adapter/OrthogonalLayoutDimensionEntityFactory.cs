using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using TileLayout.Core;
using TileLayout.Core.Models;

namespace TileLayout.AutoCAD.Adapter
{
    internal static class OrthogonalLayoutDimensionEntityFactory
    {
        internal static RotatedDimension Create(
            Database database,
            LayoutDrawingDimension dimension)
        {
            return Create(
                database,
                dimension,
                LayoutDrawingColorSettings.Default);
        }

        internal static RotatedDimension Create(
            Database database,
            LayoutDrawingDimension dimension,
            LayoutDrawingColorSettings colorSettings)
        {
            return Create(
                database,
                dimension,
                colorSettings,
                ObjectId.Null,
                OrthogonalLayoutDimensionStyle
                    .GetTransientArchitecturalTickBlockId(database));
        }

        internal static RotatedDimension CreateTransient(
            Database database,
            LayoutDrawingDimension dimension,
            LayoutDrawingColorSettings colorSettings,
            ObjectId architecturalTickBlockId)
        {
            return Create(
                database,
                dimension,
                colorSettings,
                ObjectId.Null,
                architecturalTickBlockId);
        }

        internal static RotatedDimension Create(
            Database database,
            LayoutDrawingDimension dimension,
            LayoutDrawingColorSettings colorSettings,
            ObjectId dimensionStyleId)
        {
            return Create(
                database,
                dimension,
                colorSettings,
                dimensionStyleId,
                ObjectId.Null);
        }

        private static RotatedDimension Create(
            Database database,
            LayoutDrawingDimension dimension,
            LayoutDrawingColorSettings colorSettings,
            ObjectId dimensionStyleId,
            ObjectId architecturalTickBlockId)
        {
            if (colorSettings == null)
            {
                throw new System.ArgumentNullException(nameof(colorSettings));
            }

            double rotation = dimension.Axis == TileLayoutAxis.X
                ? 0.0
                : System.Math.PI / 2.0;
            LineSegment3D measured = dimension.MeasuredSegment;
            var entity = new RotatedDimension
            {
                Rotation = rotation,
                XLine1Point = ToAcadPoint(measured.Start),
                XLine2Point = ToAcadPoint(measured.End),
                DimLinePoint = ToAcadPoint(dimension.DimensionLinePoint),
                DimensionText = dimension.DisplayText
            };
            entity.SetDatabaseDefaults(database);
            if (dimensionStyleId.IsNull)
            {
                OrthogonalLayoutDimensionStyle.ApplyTransient(
                    entity,
                    database.Textstyle,
                    architecturalTickBlockId);
            }
            else
            {
                entity.DimensionStyle = dimensionStyleId;
            }
            bool useSpecialDimensionColor =
                dimension.Kind == LayoutDrawingDimensionKind.BoundaryFeature
                || dimension.SourceId.Contains("-edge-");
            Color dimensionColor = Color.FromColorIndex(
                ColorMethod.ByAci,
                useSpecialDimensionColor
                    ? colorSettings.BoundaryFeatureDimensionColorIndex
                    : colorSettings.TileDimensionColorIndex);
            entity.Color = dimensionColor;
            entity.Dimclrd = dimensionColor;
            entity.Dimclre = dimensionColor;
            entity.Dimclrt = dimensionColor;
            return entity;
        }

        private static Point3d ToAcadPoint(Point3D point)
        {
            return new Point3d(point.X, point.Y, point.Z);
        }
    }
}
