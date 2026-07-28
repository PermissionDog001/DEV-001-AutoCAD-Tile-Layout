using System;
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
        CoincidentPoints
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
