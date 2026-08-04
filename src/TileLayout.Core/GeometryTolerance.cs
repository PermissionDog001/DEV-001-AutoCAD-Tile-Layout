using System;

namespace TileLayout.Core
{
    public static class GeometryTolerance
    {
        public const double Coordinate = 1e-6;

        // These are intentionally separate from Coordinate. Coordinate is
        // the exact-geometry tolerance used by the existing WCS validator;
        // the near-orthogonal intake path uses a bounded, fixed correction
        // envelope instead of widening exact topology checks globally.
        public const double NearOrthogonalAngleDegrees = 0.05;

        public const double NearOrthogonalAngleRadians =
            0.0008726646259971648;

        public const double NearOrthogonalMaximumEndpointCorrection = 3.0;

        public const double NearOrthogonalEndpointJoinTolerance = 3.0;

        public static bool NearlyEqual(double first, double second)
        {
            return Math.Abs(first - second) <= Coordinate;
        }
    }
}
