namespace TileLayout.Core
{
    public sealed class BoundaryCutMeasurement
    {
        internal BoundaryCutMeasurement(
            TileLayoutAxis axis,
            double actualValue,
            double recommendedMinimum,
            double? projectAbsoluteMinimum,
            ProjectCutStatus status)
        {
            Axis = axis;
            ActualValue = actualValue;
            RecommendedMinimum = recommendedMinimum;
            ProjectAbsoluteMinimum = projectAbsoluteMinimum;
            Status = status;
        }

        public TileLayoutAxis Axis { get; }

        public double ActualValue { get; }

        public double RecommendedMinimum { get; }

        public double? ProjectAbsoluteMinimum { get; }

        public ProjectCutStatus Status { get; }
    }
}
