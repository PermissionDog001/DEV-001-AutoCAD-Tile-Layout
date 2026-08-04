using System;
using System.Collections;
using System.Collections.Generic;
using TileLayout.Core.Models;

namespace TileLayout.Core
{
    public static class EngineeringRectangularLayoutCalculator
    {
        public static EngineeringRectangularLayoutResult Calculate(
            AxisAlignedRectangle room,
            EngineeringRectangularLayoutParameters parameters)
        {
            if (room == null)
            {
                throw new ArgumentNullException(nameof(room));
            }

            if (parameters == null)
            {
                throw new ArgumentNullException(nameof(parameters));
            }

            ValidateDoorOpening(room, parameters.DoorOpening);

            TileSpanMetrics xMetrics = TileSpanCalculator.Calculate(
                room.Width,
                parameters.GridTileWidth);
            TileSpanMetrics yMetrics = TileSpanCalculator.Calculate(
                room.Height,
                parameters.GridTileHeight);
            double estimatedDivisionLineCount =
                xMetrics.InternalLineCount + yMetrics.InternalLineCount;
            int maximum = TileLayoutRules.MaximumParameterizedDivisionLineCount;
            if (estimatedDivisionLineCount > maximum)
            {
                throw new TileLayoutLimitExceededException(
                    estimatedDivisionLineCount,
                    maximum);
            }

            DoorAxes doorAxes = GetDoorAxes(parameters.DoorOpening.Wall);
            AlongWallPosition position = GetAlongWallPosition(
                room,
                parameters.DoorOpening,
                doorAxes.AlongAxis);
            RoomSide defaultAlongControl = position.IsCentered
                ? GetCenteredDefaultSide(doorAxes.AlongAxis)
                : position.NearSide;

            var candidates = new List<LayoutCandidate>();
            LayoutCandidate defaultCandidate = BuildCandidate(
                "door-default",
                true,
                false,
                position.IsCentered
                    ? "Centered door uses the deterministic WCS default side."
                    : "Door position determines the nearer along-wall side.",
                room,
                parameters,
                xMetrics,
                yMetrics,
                doorAxes,
                defaultAlongControl,
                position.IsCentered,
                false);
            candidates.Add(defaultCandidate);

            if (!defaultCandidate.IsRejected
                && position.IsCentered
                && HasDistinctFlip(defaultCandidate, doorAxes.AlongAxis))
            {
                candidates.Add(
                    BuildCandidate(
                        "door-centered-flipped",
                        false,
                        true,
                        "Centered door flips only the equivalent along-wall boundary allocation.",
                        room,
                        parameters,
                        xMetrics,
                        yMetrics,
                        doorAxes,
                        Opposite(defaultAlongControl),
                        true,
                        true));
            }

            return new EngineeringRectangularLayoutResult(
                room,
                parameters,
                candidates);
        }

        private static LayoutCandidate BuildCandidate(
            string id,
            bool isDefault,
            bool isFlippedAlternative,
            string selectionReason,
            AxisAlignedRectangle room,
            EngineeringRectangularLayoutParameters parameters,
            TileSpanMetrics xMetrics,
            TileSpanMetrics yMetrics,
            DoorAxes doorAxes,
            RoomSide alongControlSide,
            bool isCentered,
            bool isCenteredFlip)
        {
            RoomSide depthHalfSide = Opposite(parameters.DoorOpening.Wall);
            RoomSide alongHalfSide = alongControlSide;
            AxisPlanBuildResult xBuild;
            AxisPlanBuildResult yBuild;
            if (doorAxes.DepthAxis == TileLayoutAxis.X)
            {
                xBuild = BuildAxisPlan(
                    TileLayoutAxis.X,
                    DoorControlledAxisRole.DoorNormal,
                    room.Width,
                    parameters.TileWidth,
                    parameters.GridTileWidth,
                    xMetrics,
                    parameters.DoorOpening.Wall,
                    depthHalfSide);
                yBuild = BuildAxisPlan(
                    TileLayoutAxis.Y,
                    DoorControlledAxisRole.AlongWall,
                    room.Height,
                    parameters.TileHeight,
                    parameters.GridTileHeight,
                    yMetrics,
                    alongControlSide,
                    alongHalfSide);
            }
            else
            {
                xBuild = BuildAxisPlan(
                    TileLayoutAxis.X,
                    DoorControlledAxisRole.AlongWall,
                    room.Width,
                    parameters.TileWidth,
                    parameters.GridTileWidth,
                    xMetrics,
                    alongControlSide,
                    alongHalfSide);
                yBuild = BuildAxisPlan(
                    TileLayoutAxis.Y,
                    DoorControlledAxisRole.DoorNormal,
                    room.Height,
                    parameters.TileHeight,
                    parameters.GridTileHeight,
                    yMetrics,
                    parameters.DoorOpening.Wall,
                    depthHalfSide);
            }

            var plans = new List<BoundaryBandPlan>(2);
            var diagnostics = new List<CandidateDiagnostic>();
            AddBuildResult(xBuild, plans, diagnostics);
            AddBuildResult(yBuild, plans, diagnostics);

            if (isCentered)
            {
                diagnostics.Add(
                    new CandidateDiagnostic(
                        isCenteredFlip
                            ? CandidateDiagnosticCode.CenteredDoorFlipped
                            : CandidateDiagnosticCode.CenteredDoorDefaultApplied,
                        CandidateDiagnosticSeverity.Information,
                        isCenteredFlip
                            ? "The centered along-wall allocation is flipped."
                            : "The centered along-wall allocation uses the fixed WCS priority.",
                        doorAxes.AlongAxis,
                        alongControlSide));
            }

            if (xBuild.Plan == null || yBuild.Plan == null)
            {
                return new LayoutCandidate(
                    id,
                    isDefault,
                    isFlippedAlternative,
                    selectionReason,
                    plans,
                    new List<LineSegment3D>(),
                    new List<TileFootprint>(),
                    diagnostics,
                    EmptyMetrics());
            }

            double[] xCoordinates = BuildCoordinates(
                room.West,
                room.East,
                xBuild.Plan.SegmentWidths);
            double[] yCoordinates = BuildCoordinates(
                room.South,
                room.North,
                yBuild.Plan.SegmentWidths);
            var divisionLines = BuildDivisionLines(
                room,
                xCoordinates,
                yCoordinates);
            var tiles = new RectangularTileFootprintCollection(
                room,
                parameters,
                xBuild.Plan.SegmentWidths,
                yBuild.Plan.SegmentWidths,
                xCoordinates,
                yCoordinates);
            LayoutCandidateMetrics metrics = BuildMetrics(
                parameters,
                xBuild.Plan,
                yBuild.Plan);

            return new LayoutCandidate(
                id,
                isDefault,
                isFlippedAlternative,
                selectionReason,
                plans,
                divisionLines,
                tiles,
                diagnostics,
                metrics);
        }

        internal static AxisPlanBuildResult BuildAxisPlan(
            TileLayoutAxis axis,
            DoorControlledAxisRole role,
            double length,
            double tileSize,
            double gridTileSize,
            TileSpanMetrics metrics,
            RoomSide controlSide,
            RoomSide halfSideWhenRedistributed)
        {
            RoomSide lowSide = GetLowSide(axis);
            RoomSide highSide = GetHighSide(axis);
            double minimumCut =
                tileSize * EngineeringLayoutRules.DefaultMinimumCutRatio;
            double remainder = metrics.Remainder;
            double grout = gridTileSize - tileSize;
            if (double.IsNaN(grout)
                || double.IsInfinity(grout)
                || grout < -GeometryTolerance.Coordinate)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(gridTileSize),
                    "The grid pitch must not be smaller than the physical tile size.");
            }

            grout = Math.Max(0.0, grout);
            double physicalRemainder = remainder <= GeometryTolerance.Coordinate
                ? 0.0
                : remainder - grout;
            int fullSpanCount = checked((int)metrics.FullSpanCount);

            if (remainder <= GeometryTolerance.Coordinate)
            {
                var segments = Repeat(gridTileSize, fullSpanCount);
                var plan = new BoundaryBandPlan(
                    axis,
                    role,
                    tileSize,
                    0.0,
                    controlSide,
                    controlSide,
                    new AxisBoundaryBand(
                        lowSide,
                        tileSize,
                        BoundaryBandKind.FullTile),
                    new AxisBoundaryBand(
                        highSide,
                        tileSize,
                        BoundaryBandKind.FullTile),
                    fullSpanCount,
                    Math.Max(0, fullSpanCount - 2),
                    false,
                    segments,
                    gridTileSize);
                return AxisPlanBuildResult.Success(
                    plan,
                    new CandidateDiagnostic(
                        CandidateDiagnosticCode.ExactTileMultiple,
                        CandidateDiagnosticSeverity.Information,
                        "The axis length is an exact tile multiple.",
                        axis));
            }

            if (physicalRemainder + GeometryTolerance.Coordinate >= minimumCut)
            {
                var segments = new List<double>(fullSpanCount + 1);
                bool controlIsLow = controlSide == lowSide;
                if (!controlIsLow)
                {
                    segments.Add(remainder);
                }

                for (int index = 0; index < fullSpanCount; index++)
                {
                    segments.Add(gridTileSize);
                }

                if (controlIsLow)
                {
                    segments.Add(remainder);
                }

                BoundaryBandKind lowKind =
                    fullSpanCount == 0 || !controlIsLow
                        ? BoundaryBandKind.NaturalRemainder
                        : BoundaryBandKind.FullTile;
                BoundaryBandKind highKind =
                    fullSpanCount == 0 || controlIsLow
                        ? BoundaryBandKind.NaturalRemainder
                        : BoundaryBandKind.FullTile;
                double lowWidth = segments[0];
                double highWidth = segments[segments.Count - 1];
                var plan = new BoundaryBandPlan(
                    axis,
                    role,
                    tileSize,
                    physicalRemainder,
                    controlSide,
                    controlSide,
                    new AxisBoundaryBand(lowSide, lowWidth, lowKind),
                    new AxisBoundaryBand(highSide, highWidth, highKind),
                    fullSpanCount,
                    Math.Max(0, fullSpanCount - 1),
                    false,
                    segments,
                    gridTileSize);
                plan = ReplaceBoundaryWidths(
                    plan,
                    Math.Max(0.0, lowWidth - grout),
                    Math.Max(0.0, highWidth - grout));
                return AxisPlanBuildResult.Success(
                    plan,
                    new CandidateDiagnostic(
                        CandidateDiagnosticCode.NaturalRemainderAccepted,
                        CandidateDiagnosticSeverity.Information,
                        "The natural remainder satisfies the default minimum cut.",
                        axis,
                        Opposite(controlSide),
                        physicalRemainder,
                        minimumCut));
            }

            if (fullSpanCount < 1)
            {
                return AxisPlanBuildResult.Failure(
                    new CandidateDiagnostic(
                        CandidateDiagnosticCode.MinimumCutNotMet,
                        CandidateDiagnosticSeverity.Rejection,
                        "The only boundary band is below the default minimum cut.",
                        axis,
                        null,
                        physicalRemainder,
                        minimumCut),
                    new CandidateDiagnostic(
                        CandidateDiagnosticCode.InsufficientFullTileForRedistribution,
                        CandidateDiagnosticSeverity.Rejection,
                        "No full tile is available for half-tile redistribution.",
                        axis));
            }

            if (physicalRemainder <= GeometryTolerance.Coordinate)
            {
                return AxisPlanBuildResult.Failure(
                    new CandidateDiagnostic(
                        CandidateDiagnosticCode.MinimumCutNotMet,
                        CandidateDiagnosticSeverity.Rejection,
                        "The remaining boundary span is consumed by the fixed wall-grout allowance.",
                        axis,
                        null,
                        physicalRemainder,
                        minimumCut));
            }

            double half = tileSize * EngineeringLayoutRules.HalfTileRatio;
            double transition = half + physicalRemainder;
            bool halfIsLow = halfSideWhenRedistributed == lowSide;
            var redistributed = new List<double>(fullSpanCount + 1);
            redistributed.Add((halfIsLow ? half : transition) + grout);
            for (int index = 0; index < fullSpanCount - 1; index++)
            {
                redistributed.Add(gridTileSize);
            }

            redistributed.Add((halfIsLow ? transition : half) + grout);
            var redistributedPlan = new BoundaryBandPlan(
                    axis,
                    role,
                    tileSize,
                    physicalRemainder,
                controlSide,
                halfSideWhenRedistributed,
                new AxisBoundaryBand(
                    lowSide,
                    redistributed[0] - grout,
                    halfIsLow
                        ? BoundaryBandKind.HalfTile
                        : BoundaryBandKind.Transition),
                new AxisBoundaryBand(
                    highSide,
                    redistributed[redistributed.Count - 1] - grout,
                    halfIsLow
                        ? BoundaryBandKind.Transition
                        : BoundaryBandKind.HalfTile),
                fullSpanCount - 1,
                fullSpanCount - 1,
                true,
                redistributed,
                gridTileSize);
            return AxisPlanBuildResult.Success(
                redistributedPlan,
                new CandidateDiagnostic(
                    CandidateDiagnosticCode.NarrowRemainderRedistributed,
                    CandidateDiagnosticSeverity.Information,
                    "A narrow natural remainder is redistributed into a half tile and a larger transition tile.",
                    axis,
                    halfSideWhenRedistributed,
                    physicalRemainder,
                    minimumCut));
        }

        private static BoundaryBandPlan ReplaceBoundaryWidths(
            BoundaryBandPlan plan,
            double lowWidth,
            double highWidth)
        {
            return new BoundaryBandPlan(
                plan.Axis,
                plan.Role,
                plan.TileSize,
                plan.NaturalRemainder,
                plan.ControlSide,
                plan.ConstructionStartSide,
                new AxisBoundaryBand(
                    plan.LowBoundary.Side,
                    lowWidth,
                    plan.LowBoundary.Kind),
                new AxisBoundaryBand(
                    plan.HighBoundary.Side,
                    highWidth,
                    plan.HighBoundary.Kind),
                plan.FullTileCount,
                plan.InteriorFullTileCount,
                plan.UsesRedistribution,
                new List<double>(plan.SegmentWidths),
                plan.GridTileSize);
        }

        private static void ValidateDoorOpening(
            AxisAlignedRectangle room,
            DoorOpening doorOpening)
        {
            bool verticalWall =
                doorOpening.Wall == RoomSide.West
                || doorOpening.Wall == RoomSide.East;
            double minimum = verticalWall ? room.South : room.West;
            double maximum = verticalWall ? room.North : room.East;
            if (doorOpening.AlongWallStart < minimum - GeometryTolerance.Coordinate
                || doorOpening.AlongWallEnd > maximum + GeometryTolerance.Coordinate)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(doorOpening),
                    "The door opening must lie on the selected room wall.");
            }
        }

        private static AlongWallPosition GetAlongWallPosition(
            AxisAlignedRectangle room,
            DoorOpening doorOpening,
            TileLayoutAxis alongAxis)
        {
            double minimum =
                alongAxis == TileLayoutAxis.X ? room.West : room.South;
            double maximum =
                alongAxis == TileLayoutAxis.X ? room.East : room.North;
            double center = Math.Max(
                minimum,
                Math.Min(maximum, doorOpening.Center));
            double lowDistance = center - minimum;
            double highDistance = maximum - center;
            bool centered =
                Math.Abs(lowDistance - highDistance)
                    <= GeometryTolerance.Coordinate;
            return new AlongWallPosition(
                centered,
                lowDistance < highDistance
                    ? GetLowSide(alongAxis)
                    : GetHighSide(alongAxis));
        }

        private static DoorAxes GetDoorAxes(RoomSide wall)
        {
            bool verticalWall = wall == RoomSide.West || wall == RoomSide.East;
            return verticalWall
                ? new DoorAxes(TileLayoutAxis.X, TileLayoutAxis.Y)
                : new DoorAxes(TileLayoutAxis.Y, TileLayoutAxis.X);
        }

        private static RoomSide GetCenteredDefaultSide(TileLayoutAxis axis)
        {
            return axis == TileLayoutAxis.X ? RoomSide.West : RoomSide.North;
        }

        private static bool HasDistinctFlip(
            LayoutCandidate candidate,
            TileLayoutAxis alongAxis)
        {
            BoundaryBandPlan plan = candidate.GetAxisPlan(alongAxis);
            return !GeometryTolerance.NearlyEqual(
                plan.LowBoundary.Width,
                plan.HighBoundary.Width);
        }

        private static RoomSide GetLowSide(TileLayoutAxis axis)
        {
            return axis == TileLayoutAxis.X ? RoomSide.West : RoomSide.South;
        }

        private static RoomSide GetHighSide(TileLayoutAxis axis)
        {
            return axis == TileLayoutAxis.X ? RoomSide.East : RoomSide.North;
        }

        private static RoomSide Opposite(RoomSide side)
        {
            switch (side)
            {
                case RoomSide.West:
                    return RoomSide.East;
                case RoomSide.East:
                    return RoomSide.West;
                case RoomSide.South:
                    return RoomSide.North;
                case RoomSide.North:
                    return RoomSide.South;
                default:
                    throw new ArgumentOutOfRangeException(nameof(side));
            }
        }

        private static List<double> Repeat(double value, int count)
        {
            var values = new List<double>(count);
            for (int index = 0; index < count; index++)
            {
                values.Add(value);
            }

            return values;
        }

        private static double[] BuildCoordinates(
            double minimum,
            double maximum,
            IReadOnlyList<double> segmentWidths)
        {
            var coordinates = new double[segmentWidths.Count + 1];
            coordinates[0] = minimum;
            double offset = 0.0;
            for (int index = 0; index < segmentWidths.Count - 1; index++)
            {
                offset += segmentWidths[index];
                coordinates[index + 1] = minimum + offset;
            }

            coordinates[coordinates.Length - 1] = maximum;
            return coordinates;
        }

        private static List<LineSegment3D> BuildDivisionLines(
            AxisAlignedRectangle room,
            IReadOnlyList<double> xCoordinates,
            IReadOnlyList<double> yCoordinates)
        {
            int count = (xCoordinates.Count - 2) + (yCoordinates.Count - 2);
            var lines = new List<LineSegment3D>(count);
            for (int index = 1; index < xCoordinates.Count - 1; index++)
            {
                double x = xCoordinates[index];
                lines.Add(
                    new LineSegment3D(
                        new Point3D(x, room.South, room.Elevation),
                        new Point3D(x, room.North, room.Elevation)));
            }

            for (int index = 1; index < yCoordinates.Count - 1; index++)
            {
                double y = yCoordinates[index];
                lines.Add(
                    new LineSegment3D(
                        new Point3D(room.West, y, room.Elevation),
                        new Point3D(room.East, y, room.Elevation)));
            }

            return lines;
        }

        private static LayoutCandidateMetrics BuildMetrics(
            EngineeringRectangularLayoutParameters parameters,
            BoundaryBandPlan xPlan,
            BoundaryBandPlan yPlan)
        {
            int nonFullColumns = CountNonFull(
                xPlan.SegmentWidths,
                parameters.TileWidth,
                parameters.GroutWidthMm);
            int nonFullRows = CountNonFull(
                yPlan.SegmentWidths,
                parameters.TileHeight,
                parameters.GroutWidthMm);
            long columnCount = xPlan.SegmentWidths.Count;
            long rowCount = yPlan.SegmentWidths.Count;
            long boundaryNonFull =
                (nonFullColumns * rowCount)
                + (nonFullRows * columnCount)
                - ((long)nonFullColumns * nonFullRows);
            double minimumBoundaryBandWidth = Math.Min(
                Math.Min(
                    xPlan.LowBoundary.Width,
                    xPlan.HighBoundary.Width),
                Math.Min(
                    yPlan.LowBoundary.Width,
                    yPlan.HighBoundary.Width));

            return new LayoutCandidateMetrics(
                0,
                0.0,
                0.0,
                boundaryNonFull,
                0,
                minimumBoundaryBandWidth,
                0,
                0,
                0,
                0);
        }

        private static int CountNonFull(
            IReadOnlyList<double> widths,
            double tileSize,
            double groutWidth)
        {
            int count = 0;
            foreach (double width in widths)
            {
                double physicalWidth = width - groutWidth;
                if (!GeometryTolerance.NearlyEqual(
                    physicalWidth,
                    tileSize))
                {
                    count++;
                }
            }

            return count;
        }

        private static LayoutCandidateMetrics EmptyMetrics()
        {
            return new LayoutCandidateMetrics(
                0,
                0.0,
                0.0,
                0,
                0,
                0.0,
                0,
                0,
                0,
                0);
        }

        private static void AddBuildResult(
            AxisPlanBuildResult result,
            ICollection<BoundaryBandPlan> plans,
            ICollection<CandidateDiagnostic> diagnostics)
        {
            if (result.Plan != null)
            {
                plans.Add(result.Plan);
            }

            foreach (CandidateDiagnostic diagnostic in result.Diagnostics)
            {
                diagnostics.Add(diagnostic);
            }
        }

        private sealed class RectangularTileFootprintCollection
            : IReadOnlyList<TileFootprint>
        {
            private readonly AxisAlignedRectangle room;
            private readonly EngineeringRectangularLayoutParameters parameters;
            private readonly IReadOnlyList<double> xWidths;
            private readonly IReadOnlyList<double> yWidths;
            private readonly IReadOnlyList<double> xCoordinates;
            private readonly IReadOnlyList<double> yCoordinates;
            private readonly int columnCount;
            private readonly int rowCount;

            public RectangularTileFootprintCollection(
                AxisAlignedRectangle room,
                EngineeringRectangularLayoutParameters parameters,
                IReadOnlyList<double> xWidths,
                IReadOnlyList<double> yWidths,
                IReadOnlyList<double> xCoordinates,
                IReadOnlyList<double> yCoordinates)
            {
                this.room = room;
                this.parameters = parameters;
                this.xWidths = xWidths;
                this.yWidths = yWidths;
                this.xCoordinates = xCoordinates;
                this.yCoordinates = yCoordinates;
                columnCount = xWidths.Count;
                rowCount = yWidths.Count;
                Count = checked(columnCount * rowCount);
            }

            public int Count { get; }

            public TileFootprint this[int index]
            {
                get
                {
                    if (index < 0 || index >= Count)
                    {
                        throw new ArgumentOutOfRangeException(nameof(index));
                    }

                    int row = index / columnCount;
                    int column = index % columnCount;
                    double west = xCoordinates[column];
                    double east = xCoordinates[column + 1];
                    double south = yCoordinates[row];
                    double north = yCoordinates[row + 1];
                    double halfGrout = parameters.GroutWidthMm / 2.0;
                    double bodyWest = west + halfGrout;
                    double bodyEast = east - halfGrout;
                    double bodySouth = south + halfGrout;
                    double bodyNorth = north - halfGrout;
                    bool boundary =
                        column == 0
                        || column == columnCount - 1
                        || row == 0
                        || row == rowCount - 1;
                    var boundarySides = new List<RoomSide>(2);
                    if (column == 0)
                    {
                        boundarySides.Add(RoomSide.West);
                    }

                    if (column == columnCount - 1)
                    {
                        boundarySides.Add(RoomSide.East);
                    }

                    if (row == 0)
                    {
                        boundarySides.Add(RoomSide.South);
                    }

                    if (row == rowCount - 1)
                    {
                        boundarySides.Add(RoomSide.North);
                    }

                    double width = Math.Max(
                        0.0,
                        xWidths[column] - parameters.GroutWidthMm);
                    double height = Math.Max(
                        0.0,
                        yWidths[row] - parameters.GroutWidthMm);
                    bool full =
                        GeometryTolerance.NearlyEqual(
                            width,
                            parameters.TileWidth)
                        && GeometryTolerance.NearlyEqual(
                            height,
                            parameters.TileHeight);
                    return new TileFootprint(
                        new List<Point3D>
                        {
                            new Point3D(bodyWest, bodySouth, room.Elevation),
                            new Point3D(bodyEast, bodySouth, room.Elevation),
                            new Point3D(bodyEast, bodyNorth, room.Elevation),
                            new Point3D(bodyWest, bodyNorth, room.Elevation)
                        },
                        boundary
                            ? TileClassification.Boundary
                            : TileClassification.Interior,
                        full,
                        false,
                        width,
                        height,
                        width * height,
                        boundarySides);
                }
            }

            public IEnumerator<TileFootprint> GetEnumerator()
            {
                for (int index = 0; index < Count; index++)
                {
                    yield return this[index];
                }
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }
        }

        internal sealed class AxisPlanBuildResult
        {
            private AxisPlanBuildResult(
                BoundaryBandPlan plan,
                IList<CandidateDiagnostic> diagnostics)
            {
                Plan = plan;
                Diagnostics = diagnostics;
            }

            public BoundaryBandPlan Plan { get; }

            public IList<CandidateDiagnostic> Diagnostics { get; }

            public static AxisPlanBuildResult Success(
                BoundaryBandPlan plan,
                CandidateDiagnostic diagnostic)
            {
                return new AxisPlanBuildResult(
                    plan,
                    new List<CandidateDiagnostic> { diagnostic });
            }

            public static AxisPlanBuildResult Failure(
                params CandidateDiagnostic[] diagnostics)
            {
                return new AxisPlanBuildResult(
                    null,
                    new List<CandidateDiagnostic>(diagnostics));
            }
        }

        private struct DoorAxes
        {
            public DoorAxes(
                TileLayoutAxis depthAxis,
                TileLayoutAxis alongAxis)
            {
                DepthAxis = depthAxis;
                AlongAxis = alongAxis;
            }

            public TileLayoutAxis DepthAxis { get; }

            public TileLayoutAxis AlongAxis { get; }
        }

        private struct AlongWallPosition
        {
            public AlongWallPosition(bool isCentered, RoomSide nearSide)
            {
                IsCentered = isCentered;
                NearSide = nearSide;
            }

            public bool IsCentered { get; }

            public RoomSide NearSide { get; }
        }
    }
}
