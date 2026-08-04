using System.Collections.Generic;
using TileLayout.Core.Models;

namespace TileLayout.Core.Tests
{
    internal static class ComplexOrthogonalBoundaryFixture
    {
        public const string SourceSha256 =
            "C654BFB1A1D7C74B91DE97E5CCF80644DB7290DFC3BC52CA5E451A171C83FBDF";

        public static AxisAlignedOrthogonalPolygon CreateRoom()
        {
            return CreateRoom(Vertices());
        }

        public static AxisAlignedRectangle CreateControlRegion()
        {
            return new AxisAlignedRectangle(
                832.19436286384735,
                6887.8193057849567,
                688.06770197356991,
                5907.04836491891);
        }

        public static DoorOpening CreateDeterministicWestDoor()
        {
            const double south = 688.06770197356991;
            const double north = 5907.04836491891;
            const double width = 600.0;
            double start = south + ((north - south - width) * 0.37);
            return new DoorOpening(RoomSide.West, start, start + width);
        }

        public static IList<Point3D> Vertices()
        {
            return new List<Point3D>
            {
                P(832.19436286384735, 688.06770197356991),
                P(6887.8193057849567, 688.067701973569),
                P(6887.8193057849567, 1213.6564359078461),
                P(7666.2137492234251, 1213.6564359078461),
                P(7666.2137492234251, 141.45541868191731),
                P(12339.692550068714, 141.45541868191731),
                P(12339.692550068714, 3275.3494004910522),
                P(11477.147362370844, 3275.3494004910522),
                P(11477.147362370844, 5907.04836491891),
                P(3693.31974640226, 5907.04836491891),
                P(3693.31974640226, 6390.5900001384452),
                P(2788.6992259339972, 6390.5900001384452),
                P(2788.6992259339972, 5907.04836491891),
                P(832.19436286384735, 5907.04836491891)
            };
        }

        public static AxisAlignedOrthogonalPolygon CreateRoom(
            IList<Point3D> vertices)
        {
            var lines = new List<LineSegment3D>();
            for (int index = 0; index < vertices.Count; index++)
            {
                lines.Add(new LineSegment3D(
                    vertices[index],
                    vertices[(index + 1) % vertices.Count]));
            }

            OrthogonalRoomValidationResult validation =
                OrthogonalRoomValidator.Validate(lines);
            if (!validation.IsValid)
            {
                throw new System.InvalidOperationException(validation.ErrorMessage);
            }

            return validation.Room;
        }

        private static Point3D P(double x, double y)
        {
            return new Point3D(x, y);
        }
    }
}
