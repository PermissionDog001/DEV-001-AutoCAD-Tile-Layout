using TileLayout.Core.Models;

namespace TileLayout.Core
{
    public sealed class OrthogonalRoomOffsetResult
    {
        private OrthogonalRoomOffsetResult(
            bool isValid,
            AxisAlignedOrthogonalPolygon room,
            string errorMessage)
        {
            IsValid = isValid;
            Room = room;
            ErrorMessage = errorMessage ?? string.Empty;
        }

        public bool IsValid { get; }

        public AxisAlignedOrthogonalPolygon Room { get; }

        public string ErrorMessage { get; }

        internal static OrthogonalRoomOffsetResult Success(
            AxisAlignedOrthogonalPolygon room)
        {
            return new OrthogonalRoomOffsetResult(true, room, string.Empty);
        }

        internal static OrthogonalRoomOffsetResult Failure(string message)
        {
            return new OrthogonalRoomOffsetResult(false, null, message);
        }
    }
}
