using System;
using System.Collections.Generic;
using System.Linq;
using TileLayout.Core;
using TileLayout.Core.Models;

namespace TileLayout.AutoCAD.Adapter
{
    public enum DoorOpeningPointError
    {
        None,
        NonFinitePoint,
        DifferentElevation,
        PointNotOnRoomWall,
        PointOutsideWallSegment,
        PointsOnDifferentWalls,
        AmbiguousRoomWall,
        CoincidentPoints,
        AutomaticRegionNotFound,
        AutomaticRegionAmbiguous
    }

    public sealed class OrthogonalDoorOpeningProjectionResult
    {
        private OrthogonalDoorOpeningProjectionResult(
            AxisAlignedRectangle controlRegion,
            DoorOpeningProjectionResult projection)
        {
            ControlRegion = controlRegion;
            Projection = projection;
        }

        public bool IsValid => ControlRegion != null
            && Projection != null
            && Projection.IsValid;

        public AxisAlignedRectangle ControlRegion { get; }

        public DoorOpeningProjectionResult Projection { get; }

        public DoorOpening Opening => Projection == null
            ? null
            : Projection.Opening;

        internal static OrthogonalDoorOpeningProjectionResult Success(
            AxisAlignedRectangle controlRegion,
            DoorOpeningProjectionResult projection)
        {
            return new OrthogonalDoorOpeningProjectionResult(
                controlRegion,
                projection);
        }

        internal static OrthogonalDoorOpeningProjectionResult Failure(
            DoorOpeningPointError error,
            string message)
        {
            return new OrthogonalDoorOpeningProjectionResult(
                null,
                DoorOpeningProjectionResult.Failure(error, message));
        }
    }

    public sealed class DoorOpeningProjectionResult
    {
        private DoorOpeningProjectionResult(
            DoorOpening opening,
            Point3D firstProjectedPoint,
            Point3D secondProjectedPoint,
            DoorOpeningPointError error,
            string errorMessage)
        {
            Opening = opening;
            FirstProjectedPoint = firstProjectedPoint;
            SecondProjectedPoint = secondProjectedPoint;
            Error = error;
            ErrorMessage = errorMessage;
        }

        public bool IsValid => Error == DoorOpeningPointError.None;

        public DoorOpening Opening { get; }

        public Point3D FirstProjectedPoint { get; }

        public Point3D SecondProjectedPoint { get; }

        public DoorOpeningPointError Error { get; }

        public string ErrorMessage { get; }

        internal static DoorOpeningProjectionResult Success(
            DoorOpening opening,
            Point3D firstProjectedPoint,
            Point3D secondProjectedPoint)
        {
            return new DoorOpeningProjectionResult(
                opening,
                firstProjectedPoint,
                secondProjectedPoint,
                DoorOpeningPointError.None,
                string.Empty);
        }

        internal static DoorOpeningProjectionResult Failure(
            DoorOpeningPointError error,
            string errorMessage)
        {
            return new DoorOpeningProjectionResult(
                null,
                default(Point3D),
                default(Point3D),
                error,
                errorMessage);
        }
    }

    public static class DoorOpeningPointAdapter
    {
        private const int WestMask = 1;
        private const int EastMask = 2;
        private const int SouthMask = 4;
        private const int NorthMask = 8;

        public static DoorOpeningProjectionResult ProjectToRoomWall(
            AxisAlignedRectangle room,
            Point3D firstPoint,
            Point3D secondPoint)
        {
            if (room == null)
            {
                throw new ArgumentNullException(nameof(room));
            }

            if (!IsFinite(firstPoint) || !IsFinite(secondPoint))
            {
                return DoorOpeningProjectionResult.Failure(
                    DoorOpeningPointError.NonFinitePoint,
                    "门洞端点必须是有限 WCS 坐标。");
            }

            if (Math.Abs(firstPoint.Z - room.Elevation)
                    > GeometryTolerance.Coordinate
                || Math.Abs(secondPoint.Z - room.Elevation)
                    > GeometryTolerance.Coordinate)
            {
                return DoorOpeningProjectionResult.Failure(
                    DoorOpeningPointError.DifferentElevation,
                    "两个门洞端点必须与房间边界位于同一 WCS 高程。");
            }

            if (Math.Abs(firstPoint.X - secondPoint.X)
                    <= GeometryTolerance.Coordinate
                && Math.Abs(firstPoint.Y - secondPoint.Y)
                    <= GeometryTolerance.Coordinate
                && Math.Abs(firstPoint.Z - secondPoint.Z)
                    <= GeometryTolerance.Coordinate)
            {
                return DoorOpeningProjectionResult.Failure(
                    DoorOpeningPointError.CoincidentPoints,
                    "两个门洞端点必须为不同点，洞口宽度必须大于坐标公差。");
            }

            PointWallMatch firstMatch = MatchPoint(room, firstPoint);
            PointWallMatch secondMatch = MatchPoint(room, secondPoint);
            DoorOpeningProjectionResult endpointFailure =
                GetEndpointFailure(firstMatch, secondMatch);
            if (endpointFailure != null)
            {
                return endpointFailure;
            }

            int commonMask = firstMatch.SegmentMask & secondMatch.SegmentMask;
            if (commonMask == 0)
            {
                return DoorOpeningProjectionResult.Failure(
                    DoorOpeningPointError.PointsOnDifferentWalls,
                    "两个门洞端点必须位于同一面房间墙。");
            }

            if (!HasSingleBit(commonMask))
            {
                return DoorOpeningProjectionResult.Failure(
                    DoorOpeningPointError.AmbiguousRoomWall,
                    "门洞端点同时匹配多面墙，无法唯一确定门洞墙。");
            }

            RoomSide wall = GetWall(commonMask);
            double firstAlong = ClampAlongWall(
                room,
                wall,
                GetAlongWallCoordinate(wall, firstPoint));
            double secondAlong = ClampAlongWall(
                room,
                wall,
                GetAlongWallCoordinate(wall, secondPoint));
            if (Math.Abs(firstAlong - secondAlong)
                <= GeometryTolerance.Coordinate)
            {
                return DoorOpeningProjectionResult.Failure(
                    DoorOpeningPointError.CoincidentPoints,
                    "两个门洞端点投影后必须为不同点，洞口宽度必须大于坐标公差。");
            }

            var opening = new DoorOpening(wall, firstAlong, secondAlong);
            return DoorOpeningProjectionResult.Success(
                opening,
                ProjectPoint(room, wall, firstAlong),
                ProjectPoint(room, wall, secondAlong));
        }

        public static OrthogonalDoorOpeningProjectionResult
            ProjectToOrthogonalRoomWall(
                AxisAlignedOrthogonalPolygon room,
                Point3D firstPoint,
                Point3D secondPoint)
        {
            return ProjectToOrthogonalRoomWall(
                room,
                firstPoint,
                secondPoint,
                GeometryTolerance.Coordinate);
        }

        /// <summary>
        /// Projects interactive door picks onto the finished face. When a
        /// plaster finished face exists, users may still naturally pick the
        /// visible original wall line; that source-boundary pick is mapped to
        /// the corresponding finished-face edge before strict room-door
        /// validation runs.
        /// </summary>
        public static OrthogonalDoorOpeningProjectionResult
            ProjectToOrthogonalRoomWall(
                AxisAlignedOrthogonalPolygon room,
                AxisAlignedOrthogonalPolygon sourceRoom,
                Point3D firstPoint,
                Point3D secondPoint,
                double boundaryPointMatchTolerance)
        {
            OrthogonalDoorOpeningProjectionResult direct =
                ProjectToOrthogonalRoomWall(
                    room,
                    firstPoint,
                    secondPoint,
                    boundaryPointMatchTolerance);
            if (direct.IsValid || sourceRoom == null)
            {
                return direct;
            }

            Point3D mappedFirst;
            Point3D mappedSecond;
            if (!TryMapSourceBoundaryPoints(
                    sourceRoom,
                    room,
                    firstPoint,
                    secondPoint,
                    boundaryPointMatchTolerance,
                    out mappedFirst,
                    out mappedSecond))
            {
                return direct;
            }

            OrthogonalDoorOpeningProjectionResult mapped =
                ProjectToOrthogonalRoomWall(
                    room,
                    mappedFirst,
                    mappedSecond,
                    boundaryPointMatchTolerance);
            return mapped.IsValid ? mapped : direct;
        }

        public static OrthogonalDoorOpeningProjectionResult
            ProjectToOrthogonalRoomWall(
                AxisAlignedOrthogonalPolygon room,
                Point3D firstPoint,
                Point3D secondPoint,
                double boundaryPointMatchTolerance)
        {
            if (room == null)
            {
                throw new ArgumentNullException(nameof(room));
            }

            if (double.IsNaN(boundaryPointMatchTolerance)
                || double.IsInfinity(boundaryPointMatchTolerance)
                || boundaryPointMatchTolerance
                    < GeometryTolerance.Coordinate)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(boundaryPointMatchTolerance));
            }

            if (!IsFinite(firstPoint) || !IsFinite(secondPoint))
            {
                return OrthogonalDoorOpeningProjectionResult.Failure(
                    DoorOpeningPointError.NonFinitePoint,
                    "门洞端点必须是有限 WCS 坐标。");
            }

            if (Math.Abs(firstPoint.Z - room.Elevation)
                    > GeometryTolerance.Coordinate
                || Math.Abs(secondPoint.Z - room.Elevation)
                    > GeometryTolerance.Coordinate)
            {
                return OrthogonalDoorOpeningProjectionResult.Failure(
                    DoorOpeningPointError.DifferentElevation,
                    "两个门洞端点必须与房间边界位于同一 WCS 高程。");
            }

            if (DistanceWithinTolerance(firstPoint, secondPoint))
            {
                return OrthogonalDoorOpeningProjectionResult.Failure(
                    DoorOpeningPointError.CoincidentPoints,
                    "两个门洞端点必须为不同点，洞口宽度必须大于坐标公差。");
            }

            List<BoundaryEdge> firstEdges = MatchBoundaryEdges(
                room,
                firstPoint,
                boundaryPointMatchTolerance);
            List<BoundaryEdge> secondEdges = MatchBoundaryEdges(
                room,
                secondPoint,
                boundaryPointMatchTolerance);
            List<BoundaryEdge> commonEdges = firstEdges
                .Where(first => secondEdges.Any(second =>
                    second.Index == first.Index))
                .ToList();
            if (commonEdges.Count == 0)
            {
                return OrthogonalDoorOpeningProjectionResult.Failure(
                    firstEdges.Count == 0 || secondEdges.Count == 0
                        ? DoorOpeningPointError.PointNotOnRoomWall
                        : DoorOpeningPointError.PointsOnDifferentWalls,
                    firstEdges.Count == 0 || secondEdges.Count == 0
                        ? "两个门洞端点都必须位于完整房间的外边界上。"
                        : "两个门洞端点必须位于同一段房间外边界上。");
            }

            if (commonEdges.Count != 1)
            {
                return OrthogonalDoorOpeningProjectionResult.Failure(
                    DoorOpeningPointError.AmbiguousRoomWall,
                    "门洞端点同时匹配多段房间外边界，无法唯一确定门洞所在墙段。");
            }

            BoundaryEdge edge = commonEdges[0];
            Point3D projectedFirst = edge.Project(firstPoint, room.Elevation);
            Point3D projectedSecond = edge.Project(secondPoint, room.Elevation);
            NeutralOrthogonalRegionPartition partition =
                NeutralOrthogonalRegionPartitioner.Create(room);
            List<RegionDoorMatch> matches = partition.Regions
                .Select(region => MatchRegionDoor(
                    region.Bounds,
                    edge,
                    projectedFirst,
                    projectedSecond,
                    boundaryPointMatchTolerance))
                .Where(match => match != null)
                .ToList();
            if (matches.Count == 0)
            {
                RegionDoorMatch spanningMatch = MatchSpanningRegionDoor(
                    partition.Regions.Select(region => region.Bounds),
                    edge,
                    projectedFirst,
                    projectedSecond,
                    boundaryPointMatchTolerance);
                if (spanningMatch != null)
                {
                    return OrthogonalDoorOpeningProjectionResult.Success(
                        spanningMatch.Region,
                        DoorOpeningProjectionResult.Success(
                            spanningMatch.Opening,
                            projectedFirst,
                            projectedSecond));
                }

                return OrthogonalDoorOpeningProjectionResult.Failure(
                    DoorOpeningPointError.AutomaticRegionNotFound,
                    "程序已识别房间外墙，但没有找到完整容纳门洞的自动邻接区域。"
                        + "请确认两点没有跨过真实凹角，且外墙内侧区域保持连续。");
            }

            if (matches.Count != 1)
            {
                return OrthogonalDoorOpeningProjectionResult.Failure(
                    DoorOpeningPointError.AutomaticRegionAmbiguous,
                    "门洞同时匹配多个自动邻接区域，当前不能客观唯一确定控制区域。"
                        + "本次不采用任一区域，也不会写入图纸。");
            }

            RegionDoorMatch selected = matches[0];
            return OrthogonalDoorOpeningProjectionResult.Success(
                selected.Region,
                DoorOpeningProjectionResult.Success(
                    selected.Opening,
                    projectedFirst,
                    projectedSecond));
        }

        private static DoorOpeningProjectionResult GetEndpointFailure(
            PointWallMatch firstMatch,
            PointWallMatch secondMatch)
        {
            if (firstMatch.SegmentMask != 0 && secondMatch.SegmentMask != 0)
            {
                return null;
            }

            if ((firstMatch.SegmentMask == 0 && firstMatch.InfiniteWallMask == 0)
                || (secondMatch.SegmentMask == 0
                    && secondMatch.InfiniteWallMask == 0))
            {
                return DoorOpeningProjectionResult.Failure(
                    DoorOpeningPointError.PointNotOnRoomWall,
                    "两个门洞端点都必须在坐标公差内靠近房间墙。");
            }

            if (firstMatch.SegmentMask == 0 || secondMatch.SegmentMask == 0)
            {
                return DoorOpeningProjectionResult.Failure(
                    DoorOpeningPointError.PointOutsideWallSegment,
                    "至少一个门洞端点超出房间墙段范围。");
            }

            return null;
        }

        private static PointWallMatch MatchPoint(
            AxisAlignedRectangle room,
            Point3D point)
        {
            int infiniteMask = 0;
            int segmentMask = 0;
            AddVerticalWallMatch(
                point,
                room.West,
                room.South,
                room.North,
                WestMask,
                ref infiniteMask,
                ref segmentMask);
            AddVerticalWallMatch(
                point,
                room.East,
                room.South,
                room.North,
                EastMask,
                ref infiniteMask,
                ref segmentMask);
            AddHorizontalWallMatch(
                point,
                room.South,
                room.West,
                room.East,
                SouthMask,
                ref infiniteMask,
                ref segmentMask);
            AddHorizontalWallMatch(
                point,
                room.North,
                room.West,
                room.East,
                NorthMask,
                ref infiniteMask,
                ref segmentMask);
            return new PointWallMatch(infiniteMask, segmentMask);
        }

        private static void AddVerticalWallMatch(
            Point3D point,
            double wallX,
            double minimumY,
            double maximumY,
            int mask,
            ref int infiniteMask,
            ref int segmentMask)
        {
            if (Math.Abs(point.X - wallX) > GeometryTolerance.Coordinate)
            {
                return;
            }

            infiniteMask |= mask;
            if (IsWithinWallRange(point.Y, minimumY, maximumY))
            {
                segmentMask |= mask;
            }
        }

        private static void AddHorizontalWallMatch(
            Point3D point,
            double wallY,
            double minimumX,
            double maximumX,
            int mask,
            ref int infiniteMask,
            ref int segmentMask)
        {
            if (Math.Abs(point.Y - wallY) > GeometryTolerance.Coordinate)
            {
                return;
            }

            infiniteMask |= mask;
            if (IsWithinWallRange(point.X, minimumX, maximumX))
            {
                segmentMask |= mask;
            }
        }

        private static bool IsWithinWallRange(
            double value,
            double minimum,
            double maximum)
        {
            return value >= minimum - GeometryTolerance.Coordinate
                && value <= maximum + GeometryTolerance.Coordinate;
        }

        private static double ClampAlongWall(
            AxisAlignedRectangle room,
            RoomSide wall,
            double value)
        {
            bool vertical = wall == RoomSide.West || wall == RoomSide.East;
            double minimum = vertical ? room.South : room.West;
            double maximum = vertical ? room.North : room.East;
            return Math.Max(minimum, Math.Min(maximum, value));
        }

        private static double GetAlongWallCoordinate(
            RoomSide wall,
            Point3D point)
        {
            return wall == RoomSide.West || wall == RoomSide.East
                ? point.Y
                : point.X;
        }

        private static Point3D ProjectPoint(
            AxisAlignedRectangle room,
            RoomSide wall,
            double alongWall)
        {
            switch (wall)
            {
                case RoomSide.West:
                    return new Point3D(room.West, alongWall, room.Elevation);
                case RoomSide.East:
                    return new Point3D(room.East, alongWall, room.Elevation);
                case RoomSide.South:
                    return new Point3D(alongWall, room.South, room.Elevation);
                case RoomSide.North:
                    return new Point3D(alongWall, room.North, room.Elevation);
                default:
                    throw new ArgumentOutOfRangeException(nameof(wall));
            }
        }

        private static RoomSide GetWall(int mask)
        {
            switch (mask)
            {
                case WestMask:
                    return RoomSide.West;
                case EastMask:
                    return RoomSide.East;
                case SouthMask:
                    return RoomSide.South;
                case NorthMask:
                    return RoomSide.North;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mask));
            }
        }

        private static bool HasSingleBit(int value)
        {
            return value != 0 && (value & (value - 1)) == 0;
        }

        private static bool IsFinite(Point3D point)
        {
            return IsFinite(point.X) && IsFinite(point.Y) && IsFinite(point.Z);
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private static bool DistanceWithinTolerance(
            Point3D first,
            Point3D second)
        {
            return Math.Abs(first.X - second.X)
                    <= GeometryTolerance.Coordinate
                && Math.Abs(first.Y - second.Y)
                    <= GeometryTolerance.Coordinate
                && Math.Abs(first.Z - second.Z)
                    <= GeometryTolerance.Coordinate;
        }

        private static List<BoundaryEdge> MatchBoundaryEdges(
            AxisAlignedOrthogonalPolygon room,
            Point3D point,
            double boundaryPointMatchTolerance)
        {
            var matches = new List<BoundaryEdge>();
            for (int index = 0; index < room.Vertices.Count; index++)
            {
                var edge = new BoundaryEdge(
                    index,
                    room.Vertices[index],
                    room.Vertices[(index + 1) % room.Vertices.Count]);
                if (edge.Contains(point, boundaryPointMatchTolerance))
                {
                    matches.Add(edge);
                }
            }

            return matches;
        }

        private static bool TryMapSourceBoundaryPoints(
            AxisAlignedOrthogonalPolygon sourceRoom,
            AxisAlignedOrthogonalPolygon finishedRoom,
            Point3D firstPoint,
            Point3D secondPoint,
            double boundaryPointMatchTolerance,
            out Point3D mappedFirst,
            out Point3D mappedSecond)
        {
            mappedFirst = default(Point3D);
            mappedSecond = default(Point3D);
            if (sourceRoom == null
                || finishedRoom == null
                || sourceRoom.Vertices.Count != finishedRoom.Vertices.Count)
            {
                return false;
            }

            List<BoundaryEdge> firstEdges = MatchBoundaryEdges(
                sourceRoom,
                firstPoint,
                boundaryPointMatchTolerance);
            List<BoundaryEdge> secondEdges = MatchBoundaryEdges(
                sourceRoom,
                secondPoint,
                boundaryPointMatchTolerance);
            List<BoundaryEdge> commonEdges = firstEdges
                .Where(first => secondEdges.Any(second =>
                    second.Index == first.Index))
                .ToList();
            if (commonEdges.Count != 1)
            {
                return false;
            }

            BoundaryEdge sourceEdge = commonEdges[0];
            BoundaryEdge finishedEdge = new BoundaryEdge(
                sourceEdge.Index,
                finishedRoom.Vertices[sourceEdge.Index],
                finishedRoom.Vertices[
                    (sourceEdge.Index + 1) % finishedRoom.Vertices.Count]);
            if (sourceEdge.IsVertical != finishedEdge.IsVertical)
            {
                return false;
            }

            return TryMapSourceBoundaryPoint(
                       sourceEdge,
                       finishedEdge,
                       firstPoint,
                       finishedRoom.Elevation,
                       boundaryPointMatchTolerance,
                       out mappedFirst)
                && TryMapSourceBoundaryPoint(
                       sourceEdge,
                       finishedEdge,
                       secondPoint,
                       finishedRoom.Elevation,
                       boundaryPointMatchTolerance,
                       out mappedSecond);
        }

        private static bool TryMapSourceBoundaryPoint(
            BoundaryEdge sourceEdge,
            BoundaryEdge finishedEdge,
            Point3D sourcePoint,
            double finishedElevation,
            double boundaryPointMatchTolerance,
            out Point3D mapped)
        {
            mapped = default(Point3D);
            double along = sourceEdge.IsVertical
                ? sourcePoint.Y
                : sourcePoint.X;
            double mappedAlong;
            if (Math.Abs(along - sourceEdge.Minimum)
                    <= boundaryPointMatchTolerance)
            {
                mappedAlong = finishedEdge.Minimum;
            }
            else if (Math.Abs(along - sourceEdge.Maximum)
                    <= boundaryPointMatchTolerance)
            {
                mappedAlong = finishedEdge.Maximum;
            }
            else
            {
                mappedAlong = along;
            }

            if (mappedAlong < finishedEdge.Minimum
                    - boundaryPointMatchTolerance
                || mappedAlong > finishedEdge.Maximum
                    + boundaryPointMatchTolerance)
            {
                return false;
            }

            mappedAlong = Math.Max(
                finishedEdge.Minimum,
                Math.Min(finishedEdge.Maximum, mappedAlong));
            mapped = finishedEdge.IsVertical
                ? new Point3D(
                    finishedEdge.Constant,
                    mappedAlong,
                    finishedElevation)
                : new Point3D(
                    mappedAlong,
                    finishedEdge.Constant,
                    finishedElevation);
            return true;
        }

        private static RegionDoorMatch MatchRegionDoor(
            AxisAlignedRectangle region,
            BoundaryEdge edge,
            Point3D first,
            Point3D second,
            double boundaryPointMatchTolerance)
        {
            RoomSide wall;
            if (edge.IsVertical)
            {
                if (GeometryTolerance.NearlyEqual(edge.Constant, region.West))
                {
                    wall = RoomSide.West;
                }
                else if (GeometryTolerance.NearlyEqual(edge.Constant, region.East))
                {
                    wall = RoomSide.East;
                }
                else
                {
                    return null;
                }

                if (!Within(
                        first.Y,
                        region.South,
                        region.North,
                        boundaryPointMatchTolerance)
                    || !Within(
                        second.Y,
                        region.South,
                        region.North,
                        boundaryPointMatchTolerance))
                {
                    return null;
                }
            }
            else
            {
                if (GeometryTolerance.NearlyEqual(edge.Constant, region.South))
                {
                    wall = RoomSide.South;
                }
                else if (GeometryTolerance.NearlyEqual(edge.Constant, region.North))
                {
                    wall = RoomSide.North;
                }
                else
                {
                    return null;
                }

                if (!Within(
                        first.X,
                        region.West,
                        region.East,
                        boundaryPointMatchTolerance)
                    || !Within(
                        second.X,
                        region.West,
                        region.East,
                        boundaryPointMatchTolerance))
                {
                    return null;
                }
            }

            double firstAlong = edge.IsVertical ? first.Y : first.X;
            double secondAlong = edge.IsVertical ? second.Y : second.X;
            return new RegionDoorMatch(
                region,
                new DoorOpening(wall, firstAlong, secondAlong));
        }

        private static RegionDoorMatch MatchSpanningRegionDoor(
            IEnumerable<AxisAlignedRectangle> regions,
            BoundaryEdge edge,
            Point3D first,
            Point3D second,
            double boundaryPointMatchTolerance)
        {
            double firstAlong = edge.IsVertical ? first.Y : first.X;
            double secondAlong = edge.IsVertical ? second.Y : second.X;
            double openingMinimum = Math.Min(firstAlong, secondAlong);
            double openingMaximum = Math.Max(firstAlong, secondAlong);
            List<RegionWallSlice> slices = regions
                .Select(region => MatchRegionWall(region, edge))
                .Where(slice => slice != null
                    && slice.Maximum
                        >= openingMinimum - boundaryPointMatchTolerance
                    && slice.Minimum
                        <= openingMaximum + boundaryPointMatchTolerance)
                .OrderBy(slice => slice.Minimum)
                .ThenBy(slice => slice.Maximum)
                .ToList();
            if (slices.Count < 2)
            {
                return null;
            }

            var contributing = new List<RegionWallSlice>();
            double coveredThrough = openingMinimum;
            foreach (RegionWallSlice slice in slices)
            {
                if (slice.Maximum
                    < coveredThrough - boundaryPointMatchTolerance)
                {
                    continue;
                }

                if (slice.Minimum
                    > coveredThrough + boundaryPointMatchTolerance)
                {
                    return null;
                }

                contributing.Add(slice);
                coveredThrough = Math.Max(coveredThrough, slice.Maximum);
                if (coveredThrough
                    >= openingMaximum - boundaryPointMatchTolerance)
                {
                    break;
                }
            }

            if (contributing.Count < 2
                || coveredThrough
                    < openingMaximum - boundaryPointMatchTolerance)
            {
                return null;
            }

            AxisAlignedRectangle control = BuildCommonControlRegion(
                edge,
                contributing);
            return control == null
                ? null
                : new RegionDoorMatch(
                    control,
                    new DoorOpening(
                        contributing[0].Wall,
                        firstAlong,
                        secondAlong));
        }

        private static RegionWallSlice MatchRegionWall(
            AxisAlignedRectangle region,
            BoundaryEdge edge)
        {
            RoomSide wall;
            double minimum;
            double maximum;
            if (edge.IsVertical)
            {
                if (GeometryTolerance.NearlyEqual(edge.Constant, region.West))
                {
                    wall = RoomSide.West;
                }
                else if (GeometryTolerance.NearlyEqual(edge.Constant, region.East))
                {
                    wall = RoomSide.East;
                }
                else
                {
                    return null;
                }

                minimum = Math.Max(edge.Minimum, region.South);
                maximum = Math.Min(edge.Maximum, region.North);
            }
            else
            {
                if (GeometryTolerance.NearlyEqual(edge.Constant, region.South))
                {
                    wall = RoomSide.South;
                }
                else if (GeometryTolerance.NearlyEqual(edge.Constant, region.North))
                {
                    wall = RoomSide.North;
                }
                else
                {
                    return null;
                }

                minimum = Math.Max(edge.Minimum, region.West);
                maximum = Math.Min(edge.Maximum, region.East);
            }

            return maximum - minimum <= GeometryTolerance.Coordinate
                ? null
                : new RegionWallSlice(region, wall, minimum, maximum);
        }

        private static AxisAlignedRectangle BuildCommonControlRegion(
            BoundaryEdge edge,
            IReadOnlyCollection<RegionWallSlice> slices)
        {
            RoomSide wall = slices.First().Wall;
            double alongMinimum = slices.Min(slice => slice.Minimum);
            double alongMaximum = slices.Max(slice => slice.Maximum);
            double west;
            double east;
            double south;
            double north;
            switch (wall)
            {
                case RoomSide.West:
                    west = edge.Constant;
                    east = slices.Min(slice => slice.Region.East);
                    south = alongMinimum;
                    north = alongMaximum;
                    break;
                case RoomSide.East:
                    west = slices.Max(slice => slice.Region.West);
                    east = edge.Constant;
                    south = alongMinimum;
                    north = alongMaximum;
                    break;
                case RoomSide.South:
                    west = alongMinimum;
                    east = alongMaximum;
                    south = edge.Constant;
                    north = slices.Min(slice => slice.Region.North);
                    break;
                case RoomSide.North:
                    west = alongMinimum;
                    east = alongMaximum;
                    south = slices.Max(slice => slice.Region.South);
                    north = edge.Constant;
                    break;
                default:
                    return null;
            }

            if (east - west <= GeometryTolerance.Coordinate
                || north - south <= GeometryTolerance.Coordinate)
            {
                return null;
            }

            return new AxisAlignedRectangle(
                west,
                east,
                south,
                north,
                slices.First().Region.Elevation);
        }

        private static bool Within(
            double value,
            double minimum,
            double maximum,
            double tolerance)
        {
            return value >= minimum - tolerance
                && value <= maximum + tolerance;
        }

        private sealed class RegionDoorMatch
        {
            public RegionDoorMatch(
                AxisAlignedRectangle region,
                DoorOpening opening)
            {
                Region = region;
                Opening = opening;
            }

            public AxisAlignedRectangle Region { get; }

            public DoorOpening Opening { get; }
        }

        private sealed class RegionWallSlice
        {
            public RegionWallSlice(
                AxisAlignedRectangle region,
                RoomSide wall,
                double minimum,
                double maximum)
            {
                Region = region;
                Wall = wall;
                Minimum = minimum;
                Maximum = maximum;
            }

            public AxisAlignedRectangle Region { get; }

            public RoomSide Wall { get; }

            public double Minimum { get; }

            public double Maximum { get; }
        }

        private struct BoundaryEdge
        {
            public BoundaryEdge(int index, Point3D first, Point3D second)
            {
                Index = index;
                IsVertical = GeometryTolerance.NearlyEqual(first.X, second.X);
                Constant = IsVertical ? first.X : first.Y;
                Minimum = IsVertical
                    ? Math.Min(first.Y, second.Y)
                    : Math.Min(first.X, second.X);
                Maximum = IsVertical
                    ? Math.Max(first.Y, second.Y)
                    : Math.Max(first.X, second.X);
            }

            public int Index { get; }

            public bool IsVertical { get; }

            public double Constant { get; }

            public double Minimum { get; }

            public double Maximum { get; }

            public bool Contains(
                Point3D point,
                double boundaryPointMatchTolerance)
            {
                double perpendicular = IsVertical ? point.X : point.Y;
                double along = IsVertical ? point.Y : point.X;
                return Math.Abs(perpendicular - Constant)
                        <= boundaryPointMatchTolerance
                    && along >= Minimum - boundaryPointMatchTolerance
                    && along <= Maximum + boundaryPointMatchTolerance;
            }

            public Point3D Project(Point3D point, double elevation)
            {
                double along = IsVertical ? point.Y : point.X;
                along = Math.Max(Minimum, Math.Min(Maximum, along));
                return IsVertical
                    ? new Point3D(Constant, along, elevation)
                    : new Point3D(along, Constant, elevation);
            }
        }

        private struct PointWallMatch
        {
            public PointWallMatch(int infiniteWallMask, int segmentMask)
            {
                InfiniteWallMask = infiniteWallMask;
                SegmentMask = segmentMask;
            }

            public int InfiniteWallMask { get; }

            public int SegmentMask { get; }
        }
    }
}
