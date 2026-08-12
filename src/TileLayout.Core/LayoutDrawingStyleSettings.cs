using System;

namespace TileLayout.Core
{
    public enum LayoutDrawingDimensionPlacement
    {
        OutsideRoom = 0,
        InsideRoom = 1
    }

    public sealed class LayoutDrawingColorSettings
    {
        public LayoutDrawingColorSettings(
            short divisionLineColorIndex,
            short tileDimensionColorIndex,
            short boundaryFeatureDimensionColorIndex,
            short plasterBoundaryColorIndex)
        {
            ValidateColorIndex(
                divisionLineColorIndex,
                nameof(divisionLineColorIndex));
            ValidateColorIndex(
                tileDimensionColorIndex,
                nameof(tileDimensionColorIndex));
            ValidateColorIndex(
                boundaryFeatureDimensionColorIndex,
                nameof(boundaryFeatureDimensionColorIndex));
            ValidateColorIndex(
                plasterBoundaryColorIndex,
                nameof(plasterBoundaryColorIndex));

            DivisionLineColorIndex = divisionLineColorIndex;
            TileDimensionColorIndex = tileDimensionColorIndex;
            BoundaryFeatureDimensionColorIndex =
                boundaryFeatureDimensionColorIndex;
            PlasterBoundaryColorIndex = plasterBoundaryColorIndex;
        }

        public short DivisionLineColorIndex { get; }

        public short TileDimensionColorIndex { get; }

        public short BoundaryFeatureDimensionColorIndex { get; }

        public short PlasterBoundaryColorIndex { get; }

        public static LayoutDrawingColorSettings Default
        {
            get
            {
                return new LayoutDrawingColorSettings(
                    3,
                    2,
                    6,
                    4);
            }
        }

        public bool IsEquivalentTo(LayoutDrawingColorSettings other)
        {
            return other != null
                && DivisionLineColorIndex
                    == other.DivisionLineColorIndex
                && TileDimensionColorIndex
                    == other.TileDimensionColorIndex
                && BoundaryFeatureDimensionColorIndex
                    == other.BoundaryFeatureDimensionColorIndex
                && PlasterBoundaryColorIndex
                    == other.PlasterBoundaryColorIndex;
        }

        private static void ValidateColorIndex(
            short colorIndex,
            string parameterName)
        {
            if (colorIndex < 1 || colorIndex > 255)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    colorIndex,
                    "ACI 颜色索引必须在 1 到 255 之间。" );
            }
        }
    }
}
