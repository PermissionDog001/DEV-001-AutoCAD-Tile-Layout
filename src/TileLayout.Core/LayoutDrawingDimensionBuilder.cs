using System;
using System.Collections.Generic;
using System.Linq;
using TileLayout.Core.Models;

namespace TileLayout.Core
{
    public static class LayoutDrawingDimensionBuilder
    {
        private const double MinimumDimensionOffsetMillimetres = 50.0;
        private const double DimensionOffsetTileRatio = 0.35;
        private const double DimensionOffsetRoomRatio = 0.12;

        public static IReadOnlyList<LayoutDrawingDimension> Build(
            AxisAlignedOrthogonalPolygon room,
            IReadOnlyList<TileFootprint> tiles,
            double tileWidth,
            double tileHeight)
        {
            return Build(
                room,
                tiles,
                tileWidth,
                tileHeight,
                LayoutDrawingDimensionPlacement.OutsideRoom,
                true);
        }

        public static IReadOnlyList<LayoutDrawingDimension> Build(
            AxisAlignedOrthogonalPolygon room,
            IReadOnlyList<TileFootprint> tiles,
            double tileWidth,
            double tileHeight,
            LayoutDrawingDimensionPlacement dimensionPlacement)
        {
            return Build(
                room,
                tiles,
                tileWidth,
                tileHeight,
                dimensionPlacement,
                true);
        }

        public static IReadOnlyList<LayoutDrawingDimension> Build(
            AxisAlignedOrthogonalPolygon room,
            IReadOnlyList<TileFootprint> tiles,
            double tileWidth,
            double tileHeight,
            LayoutDrawingDimensionPlacement dimensionPlacement,
            bool includeRoomFeatureDimensions)
        {
            if (room == null)
            {
                throw new ArgumentNullException(nameof(room));
            }

            if (tiles == null)
            {
                throw new ArgumentNullException(nameof(tiles));
            }

            if (!IsPositiveFinite(tileWidth)
                || !IsPositiveFinite(tileHeight))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(tileWidth),
                    "Tile dimensions must be finite and positive.");
            }

            if (!Enum.IsDefined(
                typeof(LayoutDrawingDimensionPlacement),
                dimensionPlacement))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(dimensionPlacement));
            }

            double baseOffset = CalculateBaseOffset(
                room,
                tileWidth,
                tileHeight);
            var dimensions = new List<LayoutDrawingDimension>();
            int tileDimensionIndex = 0;
            int featureDimensionIndex = 0;

            AddFirstRowDimensions(
                room,
                tiles,
                baseOffset,
                dimensionPlacement,
                dimensions,
                ref tileDimensionIndex);
            AddFirstColumnDimensions(
                room,
                tiles,
                baseOffset,
                dimensionPlacement,
                dimensions,
                ref tileDimensionIndex);
            AddSpecialTileDimensions(
                room,
                tiles,
                baseOffset,
                dimensionPlacement,
                dimensions,
                ref tileDimensionIndex);
            if (includeRoomFeatureDimensions)
            {
                AddBoundaryFeatureDimensions(
                    room,
                    baseOffset,
                    dimensionPlacement,
                    dimensions,
                    ref featureDimensionIndex);
            }

            return dimensions.AsReadOnly();
        }

        private static void AddFirstRowDimensions(
            AxisAlignedOrthogonalPolygon room,
            IReadOnlyList<TileFootprint> tiles,
            double baseOffset,
            LayoutDrawingDimensionPlacement dimensionPlacement,
            IList<LayoutDrawingDimension> dimensions,
            ref int dimensionIndex)
        {
            IReadOnlyList<IndexedTile> rowTiles =
                SelectRepresentativeRowTiles(room, tiles);

            foreach (IndexedTile item in rowTiles)
            {
                Edge edge = SelectBandEdge(item.Tile, true);
                if (edge.Length <= GeometryTolerance.Coordinate)
                {
                    continue;
                }

                Point3D dimensionLinePoint = DimensionPointForPlacement(
                    edge,
                    new Point3D(
                        (edge.Start.X + edge.End.X) / 2.0,
                        room.South - baseOffset,
                        edge.Start.Z),
                    dimensionPlacement);
                AddDimension(
                    dimensions,
                    "tile-" + (item.Index + 1) + "-row",
                    edge,
                    dimensionLinePoint,
                    LayoutDrawingDimensionKind.TileSize,
                    ref dimensionIndex);
            }
        }

        private static void AddFirstColumnDimensions(
            AxisAlignedOrthogonalPolygon room,
            IReadOnlyList<TileFootprint> tiles,
            double baseOffset,
            LayoutDrawingDimensionPlacement dimensionPlacement,
            IList<LayoutDrawingDimension> dimensions,
            ref int dimensionIndex)
        {
            IReadOnlyList<IndexedTile> columnTiles =
                SelectRepresentativeColumnTiles(room, tiles);

            foreach (IndexedTile item in columnTiles)
            {
                Edge edge = SelectBandEdge(item.Tile, false);
                if (edge.Length <= GeometryTolerance.Coordinate)
                {
                    continue;
                }

                Point3D dimensionLinePoint = DimensionPointForPlacement(
                    edge,
                    new Point3D(
                        room.West - baseOffset,
                        (edge.Start.Y + edge.End.Y) / 2.0,
                        edge.Start.Z),
                    dimensionPlacement);
                AddDimension(
                    dimensions,
                    "tile-" + (item.Index + 1) + "-column",
                    edge,
                    dimensionLinePoint,
                    LayoutDrawingDimensionKind.TileSize,
                    ref dimensionIndex);
            }
        }

        private static IReadOnlyList<IndexedTile> SelectRepresentativeRowTiles(
            AxisAlignedOrthogonalPolygon room,
            IReadOnlyList<TileFootprint> tiles)
        {
            return SelectRepresentativeBandTiles(
                room,
                tiles,
                true);
        }

        private static IReadOnlyList<IndexedTile>
            SelectRepresentativeColumnTiles(
                AxisAlignedOrthogonalPolygon room,
                IReadOnlyList<TileFootprint> tiles)
        {
            return SelectRepresentativeBandTiles(
                room,
                tiles,
                false);
        }

        private static IReadOnlyList<IndexedTile> SelectRepresentativeBandTiles(
            AxisAlignedOrthogonalPolygon room,
            IReadOnlyList<TileFootprint> tiles,
            bool horizontal)
        {
            double expectedSpan = horizontal
                ? room.Width
                : room.Height;
            double roomMinimum = horizontal
                ? room.South
                : room.West;
            double roomMaximum = horizontal
                ? room.North
                : room.East;
            double roomCenter = (roomMinimum + roomMaximum) / 2.0;
            var bands = new List<TileBand>();
            for (int index = 0; index < tiles.Count; index++)
            {
                TileFootprint tile = tiles[index];
                double position = horizontal
                    ? MinimumY(tile.Outline)
                    : MinimumX(tile.Outline);
                TileBand band = bands.FirstOrDefault(item =>
                    NearlyEqual(item.Position, position));
                if (band == null)
                {
                    band = new TileBand(position, index);
                    bands.Add(band);
                }

                band.Tiles.Add(new IndexedTile(tile, index));
            }

            var scores = bands
                .Select(band => new TileBandScore(
                    band,
                    CalculateLongestContinuousSpan(
                        band.Tiles,
                        horizontal),
                    band.Tiles.Any(item =>
                        IsGeneralDimensionTile(item.Tile))))
                .ToList();
            if (scores.Any(score => score.HasGeneralTile))
            {
                scores = scores
                    .Where(score => score.HasGeneralTile)
                    .ToList();
            }

            bool hasFullSpanBand = scores.Any(score =>
                IsFullSpanBand(score, expectedSpan));
            if (hasFullSpanBand)
            {
                scores = scores
                    .Where(score => IsFullSpanBand(score, expectedSpan))
                    .ToList();
            }

            TileBand selected = scores
                // Keep the established full-span rule first. Among eligible
                // bands, prefer an interior band nearest the room centre so
                // architectural tick marks do not cover a wall corner or a
                // boundary seam at the room edge.
                .OrderByDescending(score => IsFullSpanBand(
                    score,
                    expectedSpan))
                .ThenByDescending(score => IsInteriorBand(
                    score.Band.Position,
                    roomMinimum,
                    roomMaximum))
                .ThenBy(score => Math.Abs(
                    score.Band.Position - roomCenter))
                .ThenByDescending(score => score.ContinuousSpan)
                .ThenByDescending(score => score.HasGeneralTile)
                .ThenBy(score => score.Band.Position)
                .ThenBy(score => score.Band.FirstIndex)
                .Select(score => score.Band)
                .FirstOrDefault();

            if (selected == null)
            {
                return new List<IndexedTile>();
            }

            return selected.Tiles
                .OrderBy(item => horizontal
                    ? MinimumX(item.Tile.Outline)
                    : MinimumY(item.Tile.Outline))
                .ThenBy(item => horizontal
                    ? MinimumY(item.Tile.Outline)
                    : MinimumX(item.Tile.Outline))
                .ThenBy(item => item.Index)
                .ToList();
        }

        private static bool IsFullSpanBand(
            TileBandScore score,
            double expectedSpan)
        {
            return score.ContinuousSpan
                >= expectedSpan - GeometryTolerance.Coordinate;
        }

        private static bool IsInteriorBand(
            double position,
            double roomMinimum,
            double roomMaximum)
        {
            return position > roomMinimum + GeometryTolerance.Coordinate
                && position < roomMaximum - GeometryTolerance.Coordinate;
        }

        private static double CalculateLongestContinuousSpan(
            IReadOnlyList<IndexedTile> tiles,
            bool horizontal)
        {
            var intervals = tiles
                .Select(item => new AxisInterval(
                    horizontal
                        ? MinimumX(item.Tile.Outline)
                        : MinimumY(item.Tile.Outline),
                    horizontal
                        ? MaximumX(item.Tile.Outline)
                        : MaximumY(item.Tile.Outline)))
                .Where(interval => interval.End - interval.Start
                    > GeometryTolerance.Coordinate)
                .OrderBy(interval => interval.Start)
                .ThenBy(interval => interval.End)
                .ToList();
            if (intervals.Count == 0)
            {
                return 0.0;
            }

            double currentStart = intervals[0].Start;
            double currentEnd = intervals[0].End;
            double longest = 0.0;
            foreach (AxisInterval interval in intervals.Skip(1))
            {
                if (interval.Start <= currentEnd
                    + GeometryTolerance.Coordinate)
                {
                    currentEnd = Math.Max(currentEnd, interval.End);
                    continue;
                }

                longest = Math.Max(longest, currentEnd - currentStart);
                currentStart = interval.Start;
                currentEnd = interval.End;
            }

            return Math.Max(longest, currentEnd - currentStart);
        }

        private static bool IsGeneralDimensionTile(TileFootprint tile)
        {
            return tile != null
                && tile.IsFullTile
                && !tile.IsContinuousIrregular;
        }

        private static void AddSpecialTileDimensions(
            AxisAlignedOrthogonalPolygon room,
            IReadOnlyList<TileFootprint> tiles,
            double baseOffset,
            LayoutDrawingDimensionPlacement dimensionPlacement,
            IList<LayoutDrawingDimension> dimensions,
            ref int dimensionIndex)
        {
            var candidates = new List<SpecialEdgeCandidate>();
            for (int tileIndex = 0; tileIndex < tiles.Count; tileIndex++)
            {
                TileFootprint tile = tiles[tileIndex];
                if (tile.IsFullTile && !tile.IsContinuousIrregular)
                {
                    continue;
                }

                for (int axisIndex = 0; axisIndex < 2; axisIndex++)
                {
                    bool horizontal = axisIndex == 0;
                    Edge edge = SelectLongestEdge(
                        tile,
                        horizontal,
                        true);
                    if (edge.Length <= GeometryTolerance.Coordinate)
                    {
                        continue;
                    }

                    int edgeIndex = FindEdgeIndex(tile.Outline, edge);
                    bool boundaryEdge = IsTileBoundaryEdge(tile, edge);
                    candidates.Add(new SpecialEdgeCandidate(
                        tileIndex,
                        edgeIndex,
                        edge,
                        boundaryEdge,
                        (int)Math.Round(
                            edge.Length,
                            MidpointRounding.AwayFromZero)));
                }
            }

            // A special footprint contributes at most one longest edge in
            // each direction. Repeated seams do not create a new size. Keep
            // one representative for each axis/value pair, and let the
            // continuous general row/column chains own values already shown
            // there. Boundary edges are preferred so a cut against a wall
            // remains visible (for example, the width of the west column).
            int horizontalSpecialIndex = 0;
            int verticalSpecialIndex = 0;
            foreach (SpecialEdgeCandidate candidate in candidates
                .Where(item => !HasGeneralDimension(
                    dimensions,
                    item.Axis,
                    item.DisplayMillimetres))
                .GroupBy(item => new DimensionValueKey(
                    item.Axis,
                    item.DisplayMillimetres))
                .Select(group => group
                    .OrderByDescending(item => item.IsBoundaryEdge)
                    .ThenByDescending(item => item.Edge.Length)
                    .ThenBy(item => item.TileIndex)
                    .ThenBy(item => item.EdgeIndex)
                    .First())
                .OrderBy(item => item.Axis)
                .ThenBy(item => item.DisplayMillimetres)
                .ThenBy(item => item.TileIndex)
                .ThenBy(item => item.EdgeIndex))
            {
                TileFootprint tile = tiles[candidate.TileIndex];
                int chainIndex = candidate.Axis == TileLayoutAxis.X
                    ? horizontalSpecialIndex++
                    : verticalSpecialIndex++;
                double specialOffset = CalculateSpecialChainOffset(
                    baseOffset,
                    chainIndex);
                Point3D dimensionLinePoint = DimensionPointForSpecialTileEdge(
                    room,
                    candidate.Edge,
                    specialOffset,
                    dimensionPlacement);
                AddDimension(
                    dimensions,
                    "tile-" + (candidate.TileIndex + 1)
                        + "-edge-" + (candidate.EdgeIndex + 1),
                    candidate.Edge,
                    dimensionLinePoint,
                    LayoutDrawingDimensionKind.TileSize,
                    ref dimensionIndex);
            }
        }

        private static bool HasGeneralDimension(
            IList<LayoutDrawingDimension> dimensions,
            TileLayoutAxis axis,
            int displayMillimetres)
        {
            return dimensions.Any(dimension =>
                dimension.Kind == LayoutDrawingDimensionKind.TileSize
                && !dimension.SourceId.Contains("-edge-")
                && dimension.Axis == axis
                && dimension.DisplayMillimetres == displayMillimetres);
        }

        private static Edge SelectLongestEdge(
            TileFootprint tile,
            bool horizontal,
            bool preferBoundaryEdge)
        {
            IReadOnlyList<IndexedEdge> edges = Edges(tile.Outline)
                .Select((edge, index) => new IndexedEdge(edge, index))
                .Where(item => IsEdgeOnAxis(item.Edge, horizontal))
                .ToList();
            if (preferBoundaryEdge)
            {
                IReadOnlyList<IndexedEdge> boundaryEdges = edges
                    .Where(item => IsTileBoundaryEdge(tile, item.Edge))
                    .ToList();
                if (boundaryEdges.Count > 0)
                {
                    edges = boundaryEdges;
                }
            }

            return edges
                .OrderByDescending(item => item.Edge.Length)
                .ThenBy(item => item.Index)
                .Select(item => item.Edge)
                .FirstOrDefault();
        }

        private static Edge SelectBandEdge(
            TileFootprint tile,
            bool horizontal)
        {
            double fixedCoordinate = horizontal
                ? MinimumY(tile.Outline)
                : MinimumX(tile.Outline);
            return Edges(tile.Outline)
                .Where(edge => IsEdgeOnAxis(edge, horizontal)
                    && NearlyEqual(
                        horizontal ? edge.Start.Y : edge.Start.X,
                        fixedCoordinate))
                .OrderByDescending(edge => edge.Length)
                .FirstOrDefault();
        }

        private static bool IsEdgeOnAxis(Edge edge, bool horizontal)
        {
            return horizontal == IsHorizontal(edge);
        }

        private static int FindEdgeIndex(
            IReadOnlyList<Point3D> outline,
            Edge target)
        {
            IReadOnlyList<Edge> edges = Edges(outline).ToList();
            for (int index = 0; index < edges.Count; index++)
            {
                if (SameSegment(edges[index].Segment, target.Segment))
                {
                    return index;
                }
            }

            return 0;
        }

        private static double CalculateSpecialChainOffset(
            double baseOffset,
            int chainIndex)
        {
            double chainStep = Math.Max(
                baseOffset * 1.35,
                180.0);
            return (baseOffset * 2.0) + (chainStep * chainIndex);
        }

        private static void AddBoundaryFeatureDimensions(
            AxisAlignedOrthogonalPolygon room,
            double baseOffset,
            LayoutDrawingDimensionPlacement dimensionPlacement,
            IList<LayoutDrawingDimension> dimensions,
            ref int dimensionIndex)
        {
            int edgeIndex = 0;
            foreach (Edge edge in Edges(room.Vertices))
            {
                if (IsExtremeRoomEdge(room, edge))
                {
                    edgeIndex++;
                    continue;
                }

                Point3D dimensionLinePoint = OffsetFromRoomEdge(
                    room,
                    edge,
                    baseOffset * (2.0 + (edgeIndex * 0.2)),
                    dimensionPlacement);
                AddDimension(
                    dimensions,
                    "room-edge-" + (edgeIndex + 1),
                    edge,
                    dimensionLinePoint,
                    LayoutDrawingDimensionKind.BoundaryFeature,
                    ref dimensionIndex);
                edgeIndex++;
            }
        }

        private static void AddDimension(
            IList<LayoutDrawingDimension> dimensions,
            string sourceId,
            Edge edge,
            Point3D dimensionLinePoint,
            LayoutDrawingDimensionKind kind,
            ref int dimensionIndex)
        {
            if (edge.Length <= GeometryTolerance.Coordinate
                || dimensions.Any(item => item.Kind == kind
                    && SameSegment(item.MeasuredSegment, edge.Segment)))
            {
                return;
            }

            TileLayoutAxis axis = IsHorizontal(edge)
                ? TileLayoutAxis.X
                : TileLayoutAxis.Y;
            int displayMillimetres = (int)Math.Round(
                edge.Length,
                MidpointRounding.AwayFromZero);
            dimensions.Add(
                new LayoutDrawingDimension(
                    "dimension-" + (kind == LayoutDrawingDimensionKind.TileSize
                        ? "tile-"
                        : "feature-")
                        + (dimensionIndex + 1).ToString("D4"),
                    sourceId,
                    edge.Segment,
                    dimensionLinePoint,
                    axis,
                    kind,
                    displayMillimetres));
            dimensionIndex++;
        }

        private static Point3D DimensionPointForTileEdge(
            AxisAlignedOrthogonalPolygon room,
            TileFootprint tile,
            Edge edge,
            double offset,
            bool boundaryEdge,
            LayoutDrawingDimensionPlacement dimensionPlacement)
        {
            if (dimensionPlacement
                == LayoutDrawingDimensionPlacement.InsideRoom)
            {
                return Midpoint(edge);
            }

            if (boundaryEdge)
            {
                return OffsetFromRoomEdge(
                    room,
                    edge,
                    offset,
                    dimensionPlacement);
            }

            return OffsetFromPolygonEdge(tile.Outline, edge, offset);
        }

        private static Point3D DimensionPointForSpecialTileEdge(
            AxisAlignedOrthogonalPolygon room,
            Edge edge,
            double offset,
            LayoutDrawingDimensionPlacement dimensionPlacement)
        {
            if (dimensionPlacement
                == LayoutDrawingDimensionPlacement.InsideRoom)
            {
                return Midpoint(edge);
            }

            Point3D midpoint = Midpoint(edge);
            if (IsHorizontal(edge))
            {
                return new Point3D(
                    midpoint.X,
                    room.South - offset,
                    midpoint.Z);
            }

            return new Point3D(
                room.West - offset,
                midpoint.Y,
                midpoint.Z);
        }

        private static Point3D OffsetFromRoomEdge(
            AxisAlignedOrthogonalPolygon room,
            Edge edge,
            double offset,
            LayoutDrawingDimensionPlacement dimensionPlacement)
        {
            if (dimensionPlacement
                == LayoutDrawingDimensionPlacement.InsideRoom)
            {
                return Midpoint(edge);
            }

            double[] normal = OutwardNormal(room.Vertices, edge);
            Point3D midpoint = Midpoint(edge);
            return new Point3D(
                midpoint.X + normal[0] * offset,
                midpoint.Y + normal[1] * offset,
                midpoint.Z);
        }

        private static Point3D DimensionPointForPlacement(
            Edge edge,
            Point3D externalPoint,
            LayoutDrawingDimensionPlacement dimensionPlacement)
        {
            return dimensionPlacement
                == LayoutDrawingDimensionPlacement.InsideRoom
                ? Midpoint(edge)
                : externalPoint;
        }

        private static Point3D OffsetFromPolygonEdge(
            IReadOnlyList<Point3D> polygon,
            Edge edge,
            double offset)
        {
            double[] normal = OutwardNormal(polygon, edge);
            Point3D midpoint = Midpoint(edge);
            return new Point3D(
                midpoint.X + normal[0] * offset,
                midpoint.Y + normal[1] * offset,
                midpoint.Z);
        }

        private static double[] OutwardNormal(
            IReadOnlyList<Point3D> polygon,
            Edge edge)
        {
            double dx = edge.End.X - edge.Start.X;
            double dy = edge.End.Y - edge.Start.Y;
            double length = Math.Sqrt((dx * dx) + (dy * dy));
            if (length <= GeometryTolerance.Coordinate)
            {
                return new[] { 0.0, 0.0 };
            }

            bool counterClockwise = SignedArea(polygon) >= 0.0;
            double normalX = counterClockwise ? dy : -dy;
            double normalY = counterClockwise ? -dx : dx;
            return new[] { normalX / length, normalY / length };
        }

        private static double SignedArea(IReadOnlyList<Point3D> polygon)
        {
            double area = 0.0;
            for (int index = 0; index < polygon.Count; index++)
            {
                Point3D first = polygon[index];
                Point3D second = polygon[(index + 1) % polygon.Count];
                area += (first.X * second.Y) - (second.X * first.Y);
            }

            return area / 2.0;
        }

        private static bool IsExtremeRoomEdge(
            AxisAlignedOrthogonalPolygon room,
            Edge edge)
        {
            if (IsHorizontal(edge))
            {
                return NearlyEqual(edge.Start.Y, room.South)
                    || NearlyEqual(edge.Start.Y, room.North);
            }

            return NearlyEqual(edge.Start.X, room.West)
                || NearlyEqual(edge.Start.X, room.East);
        }

        private static bool IsTileBoundaryEdge(
            TileFootprint tile,
            Edge edge)
        {
            double minimumX = MinimumX(tile.Outline);
            double maximumX = MaximumX(tile.Outline);
            double minimumY = MinimumY(tile.Outline);
            double maximumY = MaximumY(tile.Outline);
            if (IsHorizontal(edge))
            {
                return (NearlyEqual(edge.Start.Y, minimumY)
                        && tile.BoundarySides.Contains(RoomSide.South))
                    || (NearlyEqual(edge.Start.Y, maximumY)
                        && tile.BoundarySides.Contains(RoomSide.North));
            }

            return (NearlyEqual(edge.Start.X, minimumX)
                    && tile.BoundarySides.Contains(RoomSide.West))
                || (NearlyEqual(edge.Start.X, maximumX)
                    && tile.BoundarySides.Contains(RoomSide.East));
        }

        private static IEnumerable<Edge> Edges(IReadOnlyList<Point3D> points)
        {
            for (int index = 0; index < points.Count; index++)
            {
                yield return new Edge(
                    points[index],
                    points[(index + 1) % points.Count]);
            }
        }

        private static Point3D Midpoint(Edge edge)
        {
            return new Point3D(
                (edge.Start.X + edge.End.X) / 2.0,
                (edge.Start.Y + edge.End.Y) / 2.0,
                (edge.Start.Z + edge.End.Z) / 2.0);
        }

        private static double CalculateBaseOffset(
            AxisAlignedOrthogonalPolygon room,
            double tileWidth,
            double tileHeight)
        {
            double tileOffset = Math.Min(tileWidth, tileHeight)
                * DimensionOffsetTileRatio;
            double roomOffset = Math.Min(room.Width, room.Height)
                * DimensionOffsetRoomRatio;
            return Math.Max(
                MinimumDimensionOffsetMillimetres,
                Math.Min(tileOffset, roomOffset));
        }

        private static bool SameSegment(
            LineSegment3D first,
            LineSegment3D second)
        {
            return (SamePoint(first.Start, second.Start)
                    && SamePoint(first.End, second.End))
                || (SamePoint(first.Start, second.End)
                    && SamePoint(first.End, second.Start));
        }

        private static bool SamePoint(Point3D first, Point3D second)
        {
            return NearlyEqual(first.X, second.X)
                && NearlyEqual(first.Y, second.Y)
                && NearlyEqual(first.Z, second.Z);
        }

        private static double MinimumX(IReadOnlyList<Point3D> points)
        {
            return points.Min(point => point.X);
        }

        private static double MaximumX(IReadOnlyList<Point3D> points)
        {
            return points.Max(point => point.X);
        }

        private static double MinimumY(IReadOnlyList<Point3D> points)
        {
            return points.Min(point => point.Y);
        }

        private static double MaximumY(IReadOnlyList<Point3D> points)
        {
            return points.Max(point => point.Y);
        }

        private static bool IsHorizontal(Edge edge)
        {
            return NearlyEqual(edge.Start.Y, edge.End.Y);
        }

        private static bool NearlyEqual(double first, double second)
        {
            return Math.Abs(first - second) <= GeometryTolerance.Coordinate;
        }

        private static bool IsPositiveFinite(double value)
        {
            return !double.IsNaN(value)
                && !double.IsInfinity(value)
                && value > GeometryTolerance.Coordinate;
        }

        private struct IndexedTile
        {
            public IndexedTile(TileFootprint tile, int index)
            {
                Tile = tile;
                Index = index;
            }

            public TileFootprint Tile { get; }

            public int Index { get; }
        }

        private sealed class TileBand
        {
            public TileBand(double position, int firstIndex)
            {
                Position = position;
                FirstIndex = firstIndex;
                Tiles = new List<IndexedTile>();
            }

            public double Position { get; }

            public int FirstIndex { get; }

            public List<IndexedTile> Tiles { get; }
        }

        private sealed class TileBandScore
        {
            public TileBandScore(
                TileBand band,
                double continuousSpan,
                bool hasGeneralTile)
            {
                Band = band;
                ContinuousSpan = continuousSpan;
                HasGeneralTile = hasGeneralTile;
            }

            public TileBand Band { get; }

            public double ContinuousSpan { get; }

            public bool HasGeneralTile { get; }
        }

        private struct AxisInterval
        {
            public AxisInterval(double start, double end)
            {
                Start = Math.Min(start, end);
                End = Math.Max(start, end);
            }

            public double Start { get; }

            public double End { get; }
        }

        private struct Edge
        {
            public Edge(Point3D start, Point3D end)
            {
                Start = start;
                End = end;
            }

            public Point3D Start { get; }

            public Point3D End { get; }

            public LineSegment3D Segment => new LineSegment3D(Start, End);

            public double Length => IsHorizontal(this)
                ? Math.Abs(End.X - Start.X)
                : Math.Abs(End.Y - Start.Y);
        }

        private struct IndexedEdge
        {
            public IndexedEdge(Edge edge, int index)
            {
                Edge = edge;
                Index = index;
            }

            public Edge Edge { get; }

            public int Index { get; }
        }

        private sealed class SpecialEdgeCandidate
        {
            public SpecialEdgeCandidate(
                int tileIndex,
                int edgeIndex,
                Edge edge,
                bool isBoundaryEdge,
                int displayMillimetres)
            {
                TileIndex = tileIndex;
                EdgeIndex = edgeIndex;
                Edge = edge;
                IsBoundaryEdge = isBoundaryEdge;
                DisplayMillimetres = displayMillimetres;
                Axis = IsHorizontal(edge)
                    ? TileLayoutAxis.X
                    : TileLayoutAxis.Y;
            }

            public int TileIndex { get; }

            public int EdgeIndex { get; }

            public Edge Edge { get; }

            public bool IsBoundaryEdge { get; }

            public int DisplayMillimetres { get; }

            public TileLayoutAxis Axis { get; }
        }

        private struct DimensionValueKey : IEquatable<DimensionValueKey>
        {
            public DimensionValueKey(
                TileLayoutAxis axis,
                int displayMillimetres)
            {
                Axis = axis;
                DisplayMillimetres = displayMillimetres;
            }

            public TileLayoutAxis Axis { get; }

            public int DisplayMillimetres { get; }

            public bool Equals(DimensionValueKey other)
            {
                return Axis == other.Axis
                    && DisplayMillimetres == other.DisplayMillimetres;
            }

            public override bool Equals(object obj)
            {
                return obj is DimensionValueKey
                    && Equals((DimensionValueKey)obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return ((int)Axis * 397) ^ DisplayMillimetres;
                }
            }
        }
    }
}
