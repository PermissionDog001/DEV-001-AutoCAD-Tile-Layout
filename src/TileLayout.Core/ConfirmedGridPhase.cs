using System;

namespace TileLayout.Core
{
    public sealed class ConfirmedGridPhase
    {
        public ConfirmedGridPhase(
            string id,
            double verticalSeamCoordinate,
            double horizontalSeamCoordinate,
            string reason,
            bool retainBelowDefaultMinimumAsPolicyUndecided = false)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("A phase id is required.", nameof(id));
            }

            if (!IsFinite(verticalSeamCoordinate)
                || !IsFinite(horizontalSeamCoordinate))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(verticalSeamCoordinate),
                    "Grid phase coordinates must be finite numbers.");
            }

            Id = id;
            VerticalSeamCoordinate = verticalSeamCoordinate;
            HorizontalSeamCoordinate = horizontalSeamCoordinate;
            Reason = reason ?? string.Empty;
            RetainBelowDefaultMinimumAsPolicyUndecided =
                retainBelowDefaultMinimumAsPolicyUndecided;
        }

        public string Id { get; }

        public double VerticalSeamCoordinate { get; }

        public double HorizontalSeamCoordinate { get; }

        public string Reason { get; }

        public bool RetainBelowDefaultMinimumAsPolicyUndecided { get; }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }
}
