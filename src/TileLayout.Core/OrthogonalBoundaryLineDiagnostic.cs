namespace TileLayout.Core
{
    public sealed class OrthogonalBoundaryLineDiagnostic
    {
        internal OrthogonalBoundaryLineDiagnostic(
            int lineNumber,
            OrthogonalBoundaryLineAxis nearestAxis,
            double angleDeviationDegrees,
            double maximumEndpointCorrection)
        {
            LineNumber = lineNumber;
            NearestAxis = nearestAxis;
            AngleDeviationDegrees = angleDeviationDegrees;
            MaximumEndpointCorrection = maximumEndpointCorrection;
        }

        public int LineNumber { get; }

        public OrthogonalBoundaryLineAxis NearestAxis { get; }

        public double AngleDeviationDegrees { get; }

        public double MaximumEndpointCorrection { get; }

        public bool WithinDirectionTolerance =>
            AngleDeviationDegrees <= GeometryTolerance.NearOrthogonalAngleDegrees;

        public bool WithinCorrectionTolerance =>
            MaximumEndpointCorrection
                <= GeometryTolerance.NearOrthogonalMaximumEndpointCorrection
                + GeometryTolerance.Coordinate;
    }
}
