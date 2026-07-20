using TileLayout.Core.Models;

namespace TileLayout.Core
{
    public sealed class RectangleValidationResult
    {
        private RectangleValidationResult(
            AxisAlignedRectangle rectangle,
            RectangleValidationError error,
            string errorMessage)
        {
            Rectangle = rectangle;
            Error = error;
            ErrorMessage = errorMessage;
        }

        public bool IsValid => Error == RectangleValidationError.None;

        public AxisAlignedRectangle Rectangle { get; }

        public RectangleValidationError Error { get; }

        public string ErrorMessage { get; }

        internal static RectangleValidationResult Success(AxisAlignedRectangle rectangle)
        {
            return new RectangleValidationResult(
                rectangle,
                RectangleValidationError.None,
                string.Empty);
        }

        internal static RectangleValidationResult Failure(
            RectangleValidationError error,
            string errorMessage)
        {
            return new RectangleValidationResult(null, error, errorMessage);
        }
    }
}
