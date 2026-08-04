using System;
using System.Collections.Generic;
using TileLayout.Core.Models;

namespace TileLayout.Core
{
    public static class DoorGeometryRecognizer
    {
        public static DoorGeometryRecognitionResult Recognize(
            IEnumerable<LineSegment3D> lines,
            IEnumerable<ArcSegment3D> arcs)
        {
            if (lines == null)
            {
                throw new ArgumentNullException(nameof(lines));
            }

            if (arcs == null)
            {
                throw new ArgumentNullException(nameof(arcs));
            }

            var lineList = new List<LineSegment3D>(lines);
            var arcList = new List<ArcSegment3D>(arcs);
            if (!ContainsOnlyFiniteGeometry(lineList, arcList))
            {
                return DoorGeometryRecognitionResult.Rejected(
                    DoorGeometryRecognitionStatus.Unsupported,
                    DoorGeometryRejectionCode.NonFiniteGeometry,
                    0);
            }

            var candidates = new List<DoorOpeningCandidate>();
            foreach (ArcSegment3D arc in arcList)
            {
                if (!IsUsableArc(arc))
                {
                    continue;
                }

                foreach (LineSegment3D line in lineList)
                {
                    AddCandidate(
                        candidates,
                        line.Start,
                        line.End,
                        arc);
                    AddCandidate(
                        candidates,
                        line.End,
                        line.Start,
                        arc);
                }
            }

            if (candidates.Count == 0)
            {
                return DoorGeometryRecognitionResult.Rejected(
                    DoorGeometryRecognitionStatus.Unsupported,
                    DoorGeometryRejectionCode.NoCompleteSingleSwingSignature,
                    0);
            }

            if (candidates.Count > 1)
            {
                return DoorGeometryRecognitionResult.Rejected(
                    DoorGeometryRecognitionStatus.Ambiguous,
                    DoorGeometryRejectionCode.MultipleDistinctSignatures,
                    candidates.Count);
            }

            return DoorGeometryRecognitionResult.High(
                candidates[0].Hinge,
                candidates[0].ClosedJamb);
        }

        private static void AddCandidate(
            ICollection<DoorOpeningCandidate> candidates,
            Point3D hinge,
            Point3D freeEnd,
            ArcSegment3D arc)
        {
            if (!AreCoincident(hinge, arc.Center))
            {
                return;
            }

            double lineLength = Distance(hinge, freeEnd);
            if (lineLength <= GeometryTolerance.Coordinate
                || Math.Abs(lineLength - arc.Radius)
                    > GeometryTolerance.Coordinate)
            {
                return;
            }

            bool freeAtStart = AreCoincident(freeEnd, arc.Start);
            bool freeAtEnd = AreCoincident(freeEnd, arc.End);
            if (freeAtStart == freeAtEnd)
            {
                return;
            }

            Point3D closedJamb = freeAtStart ? arc.End : arc.Start;
            if (AreCoincident(hinge, closedJamb))
            {
                return;
            }

            var candidate = new DoorOpeningCandidate(hinge, closedJamb);
            foreach (DoorOpeningCandidate existing in candidates)
            {
                if (AreSameOpening(existing, candidate))
                {
                    return;
                }
            }

            candidates.Add(candidate);
        }

        private static bool IsUsableArc(ArcSegment3D arc)
        {
            if (arc.Radius <= GeometryTolerance.Coordinate)
            {
                return false;
            }

            return Math.Abs(Distance(arc.Center, arc.Start) - arc.Radius)
                    <= GeometryTolerance.Coordinate
                && Math.Abs(Distance(arc.Center, arc.End) - arc.Radius)
                    <= GeometryTolerance.Coordinate
                && !AreCoincident(arc.Start, arc.End);
        }

        private static bool ContainsOnlyFiniteGeometry(
            IEnumerable<LineSegment3D> lines,
            IEnumerable<ArcSegment3D> arcs)
        {
            foreach (LineSegment3D line in lines)
            {
                if (!IsFinite(line.Start) || !IsFinite(line.End))
                {
                    return false;
                }
            }

            foreach (ArcSegment3D arc in arcs)
            {
                if (!IsFinite(arc.Center)
                    || !IsFinite(arc.Start)
                    || !IsFinite(arc.End)
                    || !IsFinite(arc.Radius))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool AreSameOpening(
            DoorOpeningCandidate first,
            DoorOpeningCandidate second)
        {
            return AreCoincident(first.Hinge, second.Hinge)
                    && AreCoincident(first.ClosedJamb, second.ClosedJamb)
                || AreCoincident(first.Hinge, second.ClosedJamb)
                    && AreCoincident(first.ClosedJamb, second.Hinge);
        }

        private static bool AreCoincident(Point3D first, Point3D second)
        {
            return Math.Abs(first.X - second.X) <= GeometryTolerance.Coordinate
                && Math.Abs(first.Y - second.Y)
                    <= GeometryTolerance.Coordinate
                && Math.Abs(first.Z - second.Z)
                    <= GeometryTolerance.Coordinate;
        }

        private static double Distance(Point3D first, Point3D second)
        {
            double deltaX = first.X - second.X;
            double deltaY = first.Y - second.Y;
            double deltaZ = first.Z - second.Z;
            return Math.Sqrt(
                (deltaX * deltaX) + (deltaY * deltaY) + (deltaZ * deltaZ));
        }

        private static bool IsFinite(Point3D point)
        {
            return IsFinite(point.X) && IsFinite(point.Y) && IsFinite(point.Z);
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private struct DoorOpeningCandidate
        {
            public DoorOpeningCandidate(Point3D hinge, Point3D closedJamb)
            {
                Hinge = hinge;
                ClosedJamb = closedJamb;
            }

            public Point3D Hinge { get; }

            public Point3D ClosedJamb { get; }
        }
    }
}
