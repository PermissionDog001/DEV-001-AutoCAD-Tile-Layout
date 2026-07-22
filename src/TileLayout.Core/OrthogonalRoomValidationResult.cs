using TileLayout.Core.Models;

namespace TileLayout.Core
{
    public sealed class OrthogonalRoomValidationResult
    {
        private OrthogonalRoomValidationResult(
            AxisAlignedOrthogonalPolygon room,
            OrthogonalRoomValidationError error,
            string errorMessage)
        {
            Room = room;
            Error = error;
            ErrorMessage = errorMessage;
        }

        public bool IsValid => Error == OrthogonalRoomValidationError.None;

        public AxisAlignedOrthogonalPolygon Room { get; }

        public OrthogonalRoomValidationError Error { get; }

        public string ErrorMessage { get; }

        internal static OrthogonalRoomValidationResult Success(
            AxisAlignedOrthogonalPolygon room)
        {
            return new OrthogonalRoomValidationResult(
                room,
                OrthogonalRoomValidationError.None,
                string.Empty);
        }

        internal static OrthogonalRoomValidationResult Failure(
            OrthogonalRoomValidationError error,
            string errorMessage)
        {
            return new OrthogonalRoomValidationResult(null, error, errorMessage);
        }
    }
}
