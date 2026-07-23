using System;
using TileLayout.Core.Models;

namespace TileLayout.Core
{
    public sealed class DoorOpening
    {
        public DoorOpening(
            RoomSide wall,
            double alongWallStart,
            double alongWallEnd)
        {
            if (!Enum.IsDefined(typeof(RoomSide), wall))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(wall),
                    "The door wall must be one of the four WCS room sides.");
            }

            if (!IsFinite(alongWallStart) || !IsFinite(alongWallEnd))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(alongWallStart),
                    "Door opening coordinates must be finite numbers.");
            }

            double minimum = Math.Min(alongWallStart, alongWallEnd);
            double maximum = Math.Max(alongWallStart, alongWallEnd);
            if (maximum - minimum <= GeometryTolerance.Coordinate)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(alongWallEnd),
                    "The door opening width must exceed the coordinate tolerance.");
            }

            Wall = wall;
            AlongWallStart = minimum;
            AlongWallEnd = maximum;
        }

        public RoomSide Wall { get; }

        public double AlongWallStart { get; }

        public double AlongWallEnd { get; }

        public double Center =>
            AlongWallStart + ((AlongWallEnd - AlongWallStart) / 2.0);

        public double Width => AlongWallEnd - AlongWallStart;

        public double GetDistanceToLowWallEnd(AxisAlignedRectangle room)
        {
            GetWallRange(room, out double minimum, out double maximum);
            return Math.Max(0.0, AlongWallStart - minimum);
        }

        public double GetDistanceToHighWallEnd(AxisAlignedRectangle room)
        {
            GetWallRange(room, out double minimum, out double maximum);
            return Math.Max(0.0, maximum - AlongWallEnd);
        }

        private void GetWallRange(
            AxisAlignedRectangle room,
            out double minimum,
            out double maximum)
        {
            if (room == null)
            {
                throw new ArgumentNullException(nameof(room));
            }

            bool verticalWall = Wall == RoomSide.West || Wall == RoomSide.East;
            minimum = verticalWall ? room.South : room.West;
            maximum = verticalWall ? room.North : room.East;
            if (AlongWallStart < minimum - GeometryTolerance.Coordinate
                || AlongWallEnd > maximum + GeometryTolerance.Coordinate)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(room),
                    "The door opening must lie on the selected room wall.");
            }
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }
}
