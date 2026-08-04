using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace TileLayout.Core
{
    public sealed class TileFootprintAssessment
    {
        internal TileFootprintAssessment(
            int tileIndex,
            TileFootprint footprint,
            IList<BoundaryCutMeasurement> measurements,
            ProjectCutStatus status,
            string reason,
            bool isEntranceVisualZone = false,
            bool isEntranceVisualBlind = false)
        {
            TileIndex = tileIndex;
            Footprint = footprint;
            Measurements = new ReadOnlyCollection<BoundaryCutMeasurement>(measurements);
            Status = status;
            Reason = reason ?? string.Empty;
            IsEntranceVisualZone = isEntranceVisualZone;
            IsEntranceVisualBlind = isEntranceVisualBlind;
        }

        public int TileIndex { get; }

        public TileFootprint Footprint { get; }

        public IReadOnlyList<BoundaryCutMeasurement> Measurements { get; }

        public ProjectCutStatus Status { get; }

        public string Reason { get; }

        public bool IsBelowRecommended =>
            Status == ProjectCutStatus.RequiresProjectPolicy
            || Status == ProjectCutStatus.RequiresUserReview
            || Status == ProjectCutStatus.BelowProjectAbsoluteMinimum;

        public bool IsBelowRecommendedButNotAbsolute =>
            Status == ProjectCutStatus.RequiresProjectPolicy
            || Status == ProjectCutStatus.RequiresUserReview;

        public bool IsEntranceVisualZone { get; }

        public bool IsEntranceVisualBlind { get; }
    }
}
