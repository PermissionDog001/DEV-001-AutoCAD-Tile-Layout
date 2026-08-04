using System;
using System.Collections.Generic;
using TileLayout.Core.Models;

namespace TileLayout.Core
{
    public static class StaticDoorGeometryRecognizer
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

                foreach (LineSegment3D hingeEdge in lineList)
                {
                    AddCandidates(
                        candidates,
                        lineList,
                        hingeEdge,
                        arc,
                        arc.Start,
                        arc.End);
                    AddCandidates(
                        candidates,
                        lineList,
                        hingeEdge,
                        arc,
                        arc.End,
                        arc.Start);
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
                candidates[0].HingeOpeningPoint,
                candidates[0].ClosedJamb);
        }

        private static void AddCandidates(
            ICollection<DoorOpeningCandidate> candidates,
            IList<LineSegment3D> lines,
            LineSegment3D hingeEdge,
            ArcSegment3D arc,
            Point3D closedJamb,
            Point3D freeArcEnd)
        {
            double thickness = Distance(hingeEdge.Start, hingeEdge.End);
            if (thickness <= GeometryTolerance.Coordinate)
            {
                return;
            }

            Point3D hingeMidpoint = Midpoint(
                hingeEdge.Start,
                hingeEdge.End);
            if (!AreCoincident(hingeMidpoint, arc.Center))
            {
                return;
            }

            double halfThickness = thickness / 2.0;
            if (arc.Radius <= halfThickness + GeometryTolerance.Coordinate)
            {
                return;
            }

            Point3D jambDirection = UnitVector(arc.Center, closedJamb);
            if (!IsFinite(jambDirection))
            {
                return;
            }

            Point3D hingeOpeningPoint = Translate(
                arc.Center,
                jambDirection,
                -halfThickness);
            Point3D otherHingePoint;
            if (AreCoincident(hingeOpeningPoint, hingeEdge.Start))
            {
                otherHingePoint = hingeEdge.End;
            }
            else if (AreCoincident(hingeOpeningPoint, hingeEdge.End))
            {
                otherHingePoint = hingeEdge.Start;
            }
            else
            {
                return;
            }

            foreach (LineSegment3D firstLongEdge in lines)
            {
                Point3D firstFreePoint;
                if (!TryGetOtherEndpoint(
                    firstLongEdge,
                    hingeOpeningPoint,
                    out firstFreePoint)
                    || Math.Abs(
                        Distance(hingeOpeningPoint, firstFreePoint)
                            - arc.Radius)
                        > GeometryTolerance.Coordinate)
                {
                    continue;
                }

                foreach (LineSegment3D secondLongEdge in lines)
                {
                    Point3D secondFreePoint;
                    if (!TryGetOtherEndpoint(
                        secondLongEdge,
                        otherHingePoint,
                        out secondFreePoint)
                        || Math.Abs(
                            Distance(otherHingePoint, secondFreePoint)
                                - arc.Radius)
                            > GeometryTolerance.Coordinate)
                    {
                        continue;
                    }

                    if (!AreEqualVectors(
                        hingeOpeningPoint,
                        firstFreePoint,
                        otherHingePoint,
                        secondFreePoint))
                    {
                        continue;
                    }

                    if (Math.Abs(
                        Distance(firstFreePoint, secondFreePoint)
                            - thickness)
                        > GeometryTolerance.Coordinate
                        || !ContainsEdge(
                            lines,
                            firstFreePoint,
                            secondFreePoint))
                    {
                        continue;
                    }

                    bool nearFirst = Distance(freeArcEnd, firstFreePoint)
                        <= halfThickness + GeometryTolerance.Coordinate;
                    bool nearSecond = Distance(freeArcEnd, secondFreePoint)
                        <= halfThickness + GeometryTolerance.Coordinate;
                    if (nearFirst == nearSecond)
                    {
                        continue;
                    }

                    AddDistinctCandidate(
                        candidates,
                        new DoorOpeningCandidate(
                            hingeOpeningPoint,
                            closedJamb));
                }
            }
        }

        private static void AddDistinctCandidate(
            ICollection<DoorOpeningCandidate> candidates,
            DoorOpeningCandidate candidate)
        {
            foreach (DoorOpeningCandidate existing in candidates)
            {
                if (AreSameOpening(existing, candidate))
                {
                    return;
                }
            }

            candidates.Add(candidate);
        }

        private static bool TryGetOtherEndpoint(
            LineSegment3D line,
            Point3D endpoint,
            out Point3D otherEndpoint)
        {
            bool startMatches = AreCoincident(line.Start, endpoint);
            bool endMatches = AreCoincident(line.End, endpoint);
            if (startMatches == endMatches)
            {
                otherEndpoint = default(Point3D);
                return false;
            }

            otherEndpoint = startMatches ? line.End : line.Start;
            return true;
        }

        private static bool ContainsEdge(
            IEnumerable<LineSegment3D> lines,
            Point3D first,
            Point3D second)
        {
            foreach (LineSegment3D line in lines)
            {
                if (AreCoincident(line.Start, first)
                        && AreCoincident(line.End, second)
                    || AreCoincident(line.Start, second)
                        && AreCoincident(line.End, first))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool AreEqualVectors(
            Point3D firstStart,
            Point3D firstEnd,
            Point3D secondStart,
            Point3D secondEnd)
        {
            return Math.Abs(
                    (firstEnd.X - firstStart.X)
                        - (secondEnd.X - secondStart.X))
                    <= GeometryTolerance.Coordinate
                && Math.Abs(
                    (firstEnd.Y - firstStart.Y)
                        - (secondEnd.Y - secondStart.Y))
                    <= GeometryTolerance.Coordinate
                && Math.Abs(
                    (firstEnd.Z - firstStart.Z)
                        - (secondEnd.Z - secondStart.Z))
                    <= GeometryTolerance.Coordinate;
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
            return AreCoincident(
                    first.HingeOpeningPoint,
                    second.HingeOpeningPoint)
                    && AreCoincident(first.ClosedJamb, second.ClosedJamb)
                || AreCoincident(
                    first.HingeOpeningPoint,
                    second.ClosedJamb)
                    && AreCoincident(
                        first.ClosedJamb,
                        second.HingeOpeningPoint);
        }

        private static Point3D Midpoint(Point3D first, Point3D second)
        {
            return new Point3D(
                (first.X + second.X) / 2.0,
                (first.Y + second.Y) / 2.0,
                (first.Z + second.Z) / 2.0);
        }

        private static Point3D UnitVector(Point3D start, Point3D end)
        {
            double length = Distance(start, end);
            if (length <= GeometryTolerance.Coordinate)
            {
                return new Point3D(double.NaN, double.NaN, double.NaN);
            }

            return new Point3D(
                (end.X - start.X) / length,
                (end.Y - start.Y) / length,
                (end.Z - start.Z) / length);
        }

        private static Point3D Translate(
            Point3D point,
            Point3D direction,
            double distance)
        {
            return new Point3D(
                point.X + (direction.X * distance),
                point.Y + (direction.Y * distance),
                point.Z + (direction.Z * distance));
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
            public DoorOpeningCandidate(
                Point3D hingeOpeningPoint,
                Point3D closedJamb)
            {
                HingeOpeningPoint = hingeOpeningPoint;
                ClosedJamb = closedJamb;
            }

            public Point3D HingeOpeningPoint { get; }

            public Point3D ClosedJamb { get; }
        }
    }
}
