using TileLayout.Core.Models;

namespace TileLayout.Core
{
    public sealed class DoorGeometryRecognitionResult
    {
        private DoorGeometryRecognitionResult(
            DoorGeometryRecognitionStatus status,
            DoorGeometryRejectionCode rejectionCode,
            Point3D firstOpeningPoint,
            Point3D secondOpeningPoint,
            int distinctCandidateCount)
        {
            Status = status;
            RejectionCode = rejectionCode;
            FirstOpeningPoint = firstOpeningPoint;
            SecondOpeningPoint = secondOpeningPoint;
            DistinctCandidateCount = distinctCandidateCount;
        }

        public DoorGeometryRecognitionStatus Status { get; }

        public DoorGeometryRejectionCode RejectionCode { get; }

        public Point3D FirstOpeningPoint { get; }

        public Point3D SecondOpeningPoint { get; }

        public int DistinctCandidateCount { get; }

        public bool IsHigh => Status == DoorGeometryRecognitionStatus.High;

        internal static DoorGeometryRecognitionResult High(
            Point3D firstOpeningPoint,
            Point3D secondOpeningPoint)
        {
            return new DoorGeometryRecognitionResult(
                DoorGeometryRecognitionStatus.High,
                DoorGeometryRejectionCode.None,
                firstOpeningPoint,
                secondOpeningPoint,
                1);
        }

        internal static DoorGeometryRecognitionResult Rejected(
            DoorGeometryRecognitionStatus status,
            DoorGeometryRejectionCode rejectionCode,
            int distinctCandidateCount)
        {
            return new DoorGeometryRecognitionResult(
                status,
                rejectionCode,
                default(Point3D),
                default(Point3D),
                distinctCandidateCount);
        }
    }
}
