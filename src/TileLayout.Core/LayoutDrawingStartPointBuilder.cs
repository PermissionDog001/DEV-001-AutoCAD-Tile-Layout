using System;
using System.Collections.Generic;
using System.Linq;
using TileLayout.Core.Models;

namespace TileLayout.Core
{
    public static class LayoutDrawingStartPointBuilder
    {
        private const string NoStartPointReason =
            "最远离门口的贴墙第一排中，没有沿墙为整砖或半砖且存在相邻灰缝的位置。";

        public static bool TryBuild(
            AxisAlignedOrthogonalPolygon room,
            LayoutCandidate candidate,
            DoorOpening doorOpening,
            double tileWidth,
            double tileHeight,
            out LayoutDrawingStartPoint startPoint,
            out string unavailableReason)
        {
            if (room == null)
            {
                throw new ArgumentNullException(nameof(room));
            }

            if (candidate == null)
            {
                throw new ArgumentNullException(nameof(candidate));
            }

            if (doorOpening == null)
            {
                throw new ArgumentNullException(nameof(doorOpening));
            }

            if (tileWidth <= GeometryTolerance.Coordinate
                || double.IsNaN(tileWidth)
                || double.IsInfinity(tileWidth)
                || tileHeight <= GeometryTolerance.Coordinate
                || double.IsNaN(tileHeight)
                || double.IsInfinity(tileHeight))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(tileWidth),
                    "Tile dimensions must be finite and greater than the coordinate tolerance.");
            }

            startPoint = null;
            unavailableReason = NoStartPointReason;

            RoomSide farWall = Opposite(doorOpening.Wall);
            RoomSide inwardDirection = GetInwardDirection(farWall);
            TileLayoutAxis alongAxis = GetAlongWallAxis(farWall);
            BoundaryBandPlan alongPlan;
            if (!candidate.TryGetAxisPlan(alongAxis, out alongPlan)
                || !IsAxisSide(alongAxis, alongPlan.ConstructionStartSide))
            {
                unavailableReason =
                    "当前方案没有有效的沿最远墙面施工方向。";
                return false;
            }

            List<StartPointPair> pairs = new List<StartPointPair>();
            for (int wallTileIndex = 0;
                wallTileIndex < candidate.Tiles.Count;
                wallTileIndex++)
            {
                TileFootprint wallTile = candidate.Tiles[wallTileIndex];
                if (!ContainsSide(wallTile, farWall))
                {
                    continue;
                }

                for (int adjacentTileIndex = 0;
                    adjacentTileIndex < candidate.Tiles.Count;
                    adjacentTileIndex++)
                {
                    if (adjacentTileIndex == wallTileIndex)
                    {
                        continue;
                    }

                    TileFootprint adjacentTile = candidate.Tiles[adjacentTileIndex];
                    if (!TryGetPairGeometry(
                        wallTile,
                        adjacentTile,
                        farWall,
                        alongAxis,
                        out double normalGap,
                        out double alongStart,
                        out double alongEnd,
                        out double wallAlongLength,
                        out double wallCoordinate,
                        out double adjacentCoordinate))
                    {
                        continue;
                    }

                    double alongLength = wallAlongLength;
                    double tileSize = alongAxis == TileLayoutAxis.X
                        ? tileWidth
                        : tileHeight;
                    LayoutDrawingStartPointTileKind tileKind;
                    if (GeometryTolerance.NearlyEqual(alongLength, tileSize))
                    {
                        tileKind = LayoutDrawingStartPointTileKind.FullTile;
                    }
                    else if (GeometryTolerance.NearlyEqual(
                        alongLength,
                        tileSize / 2.0))
                    {
                        tileKind = LayoutDrawingStartPointTileKind.HalfTile;
                    }
                    else
                    {
                        continue;
                    }

                    pairs.Add(new StartPointPair(
                        wallTileIndex,
                        adjacentTileIndex,
                        normalGap,
                        alongStart,
                        alongEnd,
                        tileKind,
                        wallCoordinate,
                        adjacentCoordinate));
                }
            }

            if (pairs.Count == 0)
            {
                return false;
            }

            StartPointPair selected = null;
            double alongCoordinate = double.NaN;
            foreach (StartPointPair pair in pairs
                .OrderBy(item => item.NormalGap)
                .ThenBy(item => GetDirectionalCoordinate(
                    item.AlongStart,
                    item.AlongEnd,
                    alongPlan.ConstructionStartSide))
                .ThenBy(item => item.WallTileIndex)
                .ThenBy(item => item.AdjacentTileIndex))
            {
                if (TryGetFourTileIntersection(
                    candidate,
                    pair,
                    alongPlan.ConstructionStartSide,
                    farWall,
                    alongAxis,
                    out alongCoordinate))
                {
                    selected = pair;
                    break;
                }
            }

            if (selected == null)
            {
                unavailableReason =
                    "最远离门口的贴墙第一排中，没有位于四块砖交界灰缝且相邻整砖或半砖的位置。";
                return false;
            }

            double normalCoordinate = GetGroutCentreCoordinate(
                candidate.Tiles[selected.WallTileIndex],
                candidate.Tiles[selected.AdjacentTileIndex],
                farWall);
            RoomSide alongWallDirection = Opposite(
                alongPlan.ConstructionStartSide);
            Point3D position = CreatePoint(
                alongAxis,
                alongCoordinate,
                normalCoordinate,
                room.Elevation);

            startPoint = new LayoutDrawingStartPoint(
                "start-point-" + (selected.WallTileIndex + 1),
                position,
                farWall,
                inwardDirection,
                alongWallDirection,
                alongAxis,
                "tile-" + (selected.WallTileIndex + 1).ToString("D4"),
                selected.WallTileKind);
            unavailableReason = string.Empty;
            return true;
        }

        private static bool TryGetFourTileIntersection(
            LayoutCandidate candidate,
            StartPointPair pair,
            RoomSide constructionStartSide,
            RoomSide farWall,
            TileLayoutAxis alongAxis,
            out double alongCoordinate)
        {
            alongCoordinate = double.NaN;
            bool useLowEnd = IsLowConstructionSide(
                alongAxis,
                constructionStartSide);
            TileFootprint wallTile = candidate.Tiles[pair.WallTileIndex];
            TileFootprint adjacentTile =
                candidate.Tiles[pair.AdjacentTileIndex];
            AxisInterval wallSegment = FindFacingSegmentAtEnd(
                wallTile,
                Opposite(farWall),
                alongAxis,
                useLowEnd
                    ? pair.AlongStart
                    : pair.AlongEnd,
                pair.NormalWallCoordinate);
            AxisInterval adjacentSegment = FindFacingSegmentAtEnd(
                adjacentTile,
                farWall,
                alongAxis,
                useLowEnd
                    ? pair.AlongStart
                    : pair.AlongEnd,
                pair.NormalAdjacentCoordinate);
            if (wallSegment == null || adjacentSegment == null)
            {
                return false;
            }

            double wallEndpoint = useLowEnd
                ? wallSegment.Start
                : wallSegment.End;
            double adjacentEndpoint = useLowEnd
                ? adjacentSegment.Start
                : adjacentSegment.End;
            if (!GeometryTolerance.NearlyEqual(
                wallEndpoint,
                adjacentEndpoint))
            {
                return false;
            }

            int wallSideNeighbourIndex;
            AxisInterval wallSideNeighbourSegment;
            if (!TryFindEndpointTile(
                candidate,
                pair.WallTileIndex,
                farWall,
                Opposite(farWall),
                alongAxis,
                useLowEnd,
                pair.NormalWallCoordinate,
                useLowEnd
                    ? wallEndpoint - pair.NormalGap
                    : wallEndpoint + pair.NormalGap,
                true,
                out wallSideNeighbourIndex,
                out wallSideNeighbourSegment))
            {
                return false;
            }

            int inwardNeighbourIndex;
            AxisInterval inwardNeighbourSegment;
            if (!TryFindEndpointTile(
                candidate,
                pair.AdjacentTileIndex,
                farWall,
                farWall,
                alongAxis,
                useLowEnd,
                pair.NormalAdjacentCoordinate,
                useLowEnd
                    ? adjacentEndpoint - pair.NormalGap
                    : adjacentEndpoint + pair.NormalGap,
                false,
                out inwardNeighbourIndex,
                out inwardNeighbourSegment))
            {
                return false;
            }

            if (wallSideNeighbourIndex == inwardNeighbourIndex
                || wallSideNeighbourIndex == pair.AdjacentTileIndex
                || inwardNeighbourIndex == pair.WallTileIndex)
            {
                return false;
            }

            bool wallNeighbourAligned = useLowEnd
                ? GeometryTolerance.NearlyEqual(
                    wallSideNeighbourSegment.End,
                    wallEndpoint - pair.NormalGap)
                : GeometryTolerance.NearlyEqual(
                    wallSideNeighbourSegment.Start,
                    wallEndpoint + pair.NormalGap);
            bool inwardNeighbourAligned = useLowEnd
                ? GeometryTolerance.NearlyEqual(
                    inwardNeighbourSegment.End,
                    adjacentEndpoint - pair.NormalGap)
                : GeometryTolerance.NearlyEqual(
                    inwardNeighbourSegment.Start,
                    adjacentEndpoint + pair.NormalGap);
            if (!wallNeighbourAligned || !inwardNeighbourAligned)
            {
                return false;
            }

            if (!GeometryTolerance.NearlyEqual(
                    wallSideNeighbourSegment.Coordinate,
                    pair.NormalWallCoordinate)
                || !GeometryTolerance.NearlyEqual(
                    inwardNeighbourSegment.Coordinate,
                    pair.NormalAdjacentCoordinate))
            {
                return false;
            }

            alongCoordinate = useLowEnd
                ? (wallSideNeighbourSegment.End + wallSegment.Start) / 2.0
                : (wallSegment.End + wallSideNeighbourSegment.Start) / 2.0;
            return true;
        }

        private static bool TryFindEndpointTile(
            LayoutCandidate candidate,
            int excludedTileIndex,
            RoomSide farWall,
            RoomSide facingSide,
            TileLayoutAxis alongAxis,
            bool useLowEnd,
            double expectedFixedCoordinate,
            double expectedEndpoint,
            bool requiresFarWallBoundary,
            out int tileIndex,
            out AxisInterval segment)
        {
            tileIndex = -1;
            segment = null;
            for (int index = 0; index < candidate.Tiles.Count; index++)
            {
                if (index == excludedTileIndex)
                {
                    continue;
                }

                TileFootprint tile = candidate.Tiles[index];
                if (requiresFarWallBoundary
                    ? !ContainsSide(tile, farWall)
                    : ContainsSide(tile, farWall))
                {
                    continue;
                }

                AxisInterval match = FindFacingSegmentAtEnd(
                    tile,
                    facingSide,
                    alongAxis,
                    expectedEndpoint,
                    expectedFixedCoordinate);
                if (match == null)
                {
                    continue;
                }

                bool endpointMatches = useLowEnd
                    ? GeometryTolerance.NearlyEqual(
                        match.End,
                        expectedEndpoint)
                    : GeometryTolerance.NearlyEqual(
                        match.Start,
                        expectedEndpoint);
                if (!endpointMatches)
                {
                    continue;
                }

                tileIndex = index;
                segment = match;
                return true;
            }

            return false;
        }

        private static AxisInterval FindFacingSegmentAtEnd(
            TileFootprint tile,
            RoomSide side,
            TileLayoutAxis alongAxis,
            double endpoint,
            double expectedFixedCoordinate)
        {
            foreach (AxisInterval segment in GetFacingSegments(
                tile,
                side,
                alongAxis))
            {
                if (!GeometryTolerance.NearlyEqual(
                    segment.Coordinate,
                    expectedFixedCoordinate))
                {
                    continue;
                }

                if (GeometryTolerance.NearlyEqual(segment.Start, endpoint)
                    || GeometryTolerance.NearlyEqual(segment.End, endpoint))
                {
                    return segment;
                }
            }

            return null;
        }

        private static bool TryGetPairGeometry(
            TileFootprint wallTile,
            TileFootprint adjacentTile,
            RoomSide farWall,
            TileLayoutAxis alongAxis,
            out double normalGap,
            out double alongStart,
            out double alongEnd,
            out double wallAlongLength,
            out double wallCoordinate,
            out double adjacentCoordinate)
        {
            var wallSegments = GetFacingSegments(
                wallTile,
                Opposite(farWall),
                alongAxis);
            var adjacentSegments = GetFacingSegments(
                adjacentTile,
                farWall,
                alongAxis);
            normalGap = double.PositiveInfinity;
            alongStart = double.NaN;
            alongEnd = double.NaN;
            wallAlongLength = double.NaN;
            wallCoordinate = double.NaN;
            adjacentCoordinate = double.NaN;
            double longestOverlap = 0.0;
            foreach (AxisInterval wallSegment in wallSegments)
            {
                foreach (AxisInterval adjacentSegment in adjacentSegments)
                {
                    double overlapStart = Math.Max(
                        wallSegment.Start,
                        adjacentSegment.Start);
                    double overlapEnd = Math.Min(
                        wallSegment.End,
                        adjacentSegment.End);
                    double overlap = overlapEnd - overlapStart;
                    if (overlap <= GeometryTolerance.Coordinate)
                    {
                        continue;
                    }

                    double gap = GetNormalGap(
                        farWall,
                        wallSegment.Coordinate,
                        adjacentSegment.Coordinate);
                    if (gap < -GeometryTolerance.Coordinate)
                    {
                        continue;
                    }

                    if (gap < normalGap
                        || GeometryTolerance.NearlyEqual(gap, normalGap)
                            && overlap > longestOverlap)
                    {
                        normalGap = gap;
                        alongStart = overlapStart;
                        alongEnd = overlapEnd;
                        wallAlongLength = wallSegment.End
                            - wallSegment.Start;
                        wallCoordinate = wallSegment.Coordinate;
                        adjacentCoordinate = adjacentSegment.Coordinate;
                        longestOverlap = overlap;
                    }
                }
            }

            return !double.IsPositiveInfinity(normalGap)
                && GeometryTolerance.NearlyEqual(
                    alongEnd - alongStart,
                    wallAlongLength);
        }

        private static double GetGroutCentreCoordinate(
            TileFootprint wallTile,
            TileFootprint adjacentTile,
            RoomSide farWall)
        {
            GetBounds(wallTile, out double wallWest, out double wallEast,
                out double wallSouth, out double wallNorth);
            GetBounds(adjacentTile, out double adjacentWest,
                out double adjacentEast, out double adjacentSouth,
                out double adjacentNorth);

            switch (farWall)
            {
                case RoomSide.South:
                    return wallNorth + ((adjacentSouth - wallNorth) / 2.0);
                case RoomSide.North:
                    return wallSouth + ((adjacentNorth - wallSouth) / 2.0);
                case RoomSide.West:
                    return wallEast + ((adjacentWest - wallEast) / 2.0);
                case RoomSide.East:
                    return wallWest + ((adjacentEast - wallWest) / 2.0);
                default:
                    throw new ArgumentOutOfRangeException(nameof(farWall));
            }
        }

        private static Point3D CreatePoint(
            TileLayoutAxis alongAxis,
            double alongCoordinate,
            double normalCoordinate,
            double elevation)
        {
            return alongAxis == TileLayoutAxis.X
                ? new Point3D(alongCoordinate, normalCoordinate, elevation)
                : new Point3D(normalCoordinate, alongCoordinate, elevation);
        }

        private static double GetDirectionalCoordinate(
            double start,
            double end,
            RoomSide direction)
        {
            return direction == RoomSide.East || direction == RoomSide.North
                ? -((start + end) / 2.0)
                : (start + end) / 2.0;
        }

        private static IList<AxisInterval> GetFacingSegments(
            TileFootprint tile,
            RoomSide side,
            TileLayoutAxis alongAxis)
        {
            GetBounds(tile, out double west, out double east,
                out double south, out double north);
            double coordinate = side == RoomSide.West
                ? west
                : side == RoomSide.East
                    ? east
                    : side == RoomSide.South
                        ? south
                        : north;
            bool horizontal = side == RoomSide.South
                || side == RoomSide.North;
            var segments = new List<AxisInterval>();
            for (int index = 0; index < tile.Outline.Count; index++)
            {
                Point3D start = tile.Outline[index];
                Point3D end = tile.Outline[(index + 1) % tile.Outline.Count];
                bool matchingOrientation = horizontal
                    ? Math.Abs(start.Y - end.Y)
                        <= GeometryTolerance.Coordinate
                    : Math.Abs(start.X - end.X)
                        <= GeometryTolerance.Coordinate;
                double fixedCoordinate = horizontal ? start.Y : start.X;
                if (!matchingOrientation
                    || Math.Abs(fixedCoordinate - coordinate)
                        > GeometryTolerance.Coordinate)
                {
                    continue;
                }

                double startCoordinate = alongAxis == TileLayoutAxis.X
                    ? start.X
                    : start.Y;
                double endCoordinate = alongAxis == TileLayoutAxis.X
                    ? end.X
                    : end.Y;
                if (endCoordinate - startCoordinate < 0.0)
                {
                    double swap = startCoordinate;
                    startCoordinate = endCoordinate;
                    endCoordinate = swap;
                }

                if (endCoordinate - startCoordinate
                    > GeometryTolerance.Coordinate)
                {
                    segments.Add(new AxisInterval(
                        startCoordinate,
                        endCoordinate,
                        fixedCoordinate));
                }
            }

            return segments;
        }

        private static double GetNormalGap(
            RoomSide farWall,
            double wallCoordinate,
            double adjacentCoordinate)
        {
            switch (farWall)
            {
                case RoomSide.South:
                case RoomSide.West:
                    return adjacentCoordinate - wallCoordinate;
                case RoomSide.North:
                case RoomSide.East:
                    return wallCoordinate - adjacentCoordinate;
                default:
                    throw new ArgumentOutOfRangeException(nameof(farWall));
            }
        }

        private static void GetBounds(
            TileFootprint tile,
            out double west,
            out double east,
            out double south,
            out double north)
        {
            west = double.PositiveInfinity;
            east = double.NegativeInfinity;
            south = double.PositiveInfinity;
            north = double.NegativeInfinity;
            foreach (Point3D point in tile.Outline)
            {
                west = Math.Min(west, point.X);
                east = Math.Max(east, point.X);
                south = Math.Min(south, point.Y);
                north = Math.Max(north, point.Y);
            }
        }

        private static bool ContainsSide(TileFootprint tile, RoomSide side)
        {
            return tile.BoundarySides.Contains(side);
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

        private static RoomSide GetInwardDirection(RoomSide farWall)
        {
            switch (farWall)
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
                    throw new ArgumentOutOfRangeException(nameof(farWall));
            }
        }

        private static TileLayoutAxis GetAlongWallAxis(RoomSide farWall)
        {
            return farWall == RoomSide.West || farWall == RoomSide.East
                ? TileLayoutAxis.Y
                : TileLayoutAxis.X;
        }

        private static bool IsAxisSide(TileLayoutAxis axis, RoomSide side)
        {
            return axis == TileLayoutAxis.X
                ? side == RoomSide.West || side == RoomSide.East
                : side == RoomSide.South || side == RoomSide.North;
        }

        private static bool IsLowConstructionSide(
            TileLayoutAxis axis,
            RoomSide side)
        {
            return axis == TileLayoutAxis.X
                ? side == RoomSide.West
                : side == RoomSide.South;
        }

        private sealed class StartPointPair
        {
            public StartPointPair(
                int wallTileIndex,
                int adjacentTileIndex,
                double normalGap,
                double alongStart,
                double alongEnd,
                LayoutDrawingStartPointTileKind wallTileKind,
                double normalWallCoordinate,
                double normalAdjacentCoordinate)
            {
                WallTileIndex = wallTileIndex;
                AdjacentTileIndex = adjacentTileIndex;
                NormalGap = normalGap;
                AlongStart = alongStart;
                AlongEnd = alongEnd;
                WallTileKind = wallTileKind;
                NormalWallCoordinate = normalWallCoordinate;
                NormalAdjacentCoordinate = normalAdjacentCoordinate;
            }

            public int WallTileIndex { get; }

            public int AdjacentTileIndex { get; }

            public double NormalGap { get; }

            public double AlongStart { get; }

            public double AlongEnd { get; }

            public LayoutDrawingStartPointTileKind WallTileKind { get; }

            public double NormalWallCoordinate { get; }

            public double NormalAdjacentCoordinate { get; }
        }

        private sealed class AxisInterval
        {
            public AxisInterval(
                double start,
                double end,
                double coordinate)
            {
                Start = start;
                End = end;
                Coordinate = coordinate;
            }

            public double Start { get; }

            public double End { get; }

            public double Coordinate { get; }
        }
    }
}
