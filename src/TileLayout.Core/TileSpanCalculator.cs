using System;

namespace TileLayout.Core
{
    internal static class TileSpanCalculator
    {
        public static TileSpanMetrics Calculate(double length, double tileSize)
        {
            double quotient = length / tileSize;
            double nearestInteger = Math.Round(quotient);
            bool isMultipleWithinTolerance =
                !double.IsInfinity(quotient)
                && Math.Abs(length - (nearestInteger * tileSize))
                    <= GeometryTolerance.Coordinate;

            double fullSpanCount = isMultipleWithinTolerance
                ? nearestInteger
                : Math.Floor(quotient);
            double internalLineCount = isMultipleWithinTolerance
                ? Math.Max(0.0, nearestInteger - 1.0)
                : fullSpanCount;
            double remainder = isMultipleWithinTolerance
                ? 0.0
                : length - (fullSpanCount * tileSize);

            return new TileSpanMetrics(
                fullSpanCount,
                internalLineCount,
                remainder);
        }
    }

    internal struct TileSpanMetrics
    {
        public TileSpanMetrics(
            double fullSpanCount,
            double internalLineCount,
            double remainder)
        {
            FullSpanCount = fullSpanCount;
            InternalLineCount = internalLineCount;
            Remainder = remainder;
        }

        public double FullSpanCount { get; }

        public double InternalLineCount { get; }

        public double Remainder { get; }
    }
}
