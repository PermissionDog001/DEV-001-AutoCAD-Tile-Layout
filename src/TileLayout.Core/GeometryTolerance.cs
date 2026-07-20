using System;

namespace TileLayout.Core
{
    public static class GeometryTolerance
    {
        public const double Coordinate = 1e-6;

        public static bool NearlyEqual(double first, double second)
        {
            return Math.Abs(first - second) <= Coordinate;
        }
    }
}
