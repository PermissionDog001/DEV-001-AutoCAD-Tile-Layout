using System.Collections.Generic;
using System.Collections.ObjectModel;
using TileLayout.Core.Models;

namespace TileLayout.Core
{
    public sealed class TileFootprint
    {
        internal TileFootprint(
            IList<Point3D> outline,
            TileClassification classification,
            bool isFullTile,
            bool isContinuousIrregular,
            double nominalWidth,
            double nominalHeight,
            double area,
            IList<RoomSide> boundarySides)
        {
            Outline = new ReadOnlyCollection<Point3D>(outline);
            Classification = classification;
            IsFullTile = isFullTile;
            IsContinuousIrregular = isContinuousIrregular;
            NominalWidth = nominalWidth;
            NominalHeight = nominalHeight;
            Area = area;
            BoundarySides = new ReadOnlyCollection<RoomSide>(boundarySides);
        }

        public IReadOnlyList<Point3D> Outline { get; }

        public TileClassification Classification { get; }

        public bool IsFullTile { get; }

        public bool IsContinuousIrregular { get; }

        public double NominalWidth { get; }

        public double NominalHeight { get; }

        public double Area { get; }

        public IReadOnlyList<RoomSide> BoundarySides { get; }
    }
}
