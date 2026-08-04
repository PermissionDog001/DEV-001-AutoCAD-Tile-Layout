using System;
using System.Collections.Generic;
using TileLayout.Core;
using TileLayout.Core.Models;

namespace TileLayout.AutoCAD.Adapter
{
    public sealed class OrthogonalDecisionInputSession
    {
        public OrthogonalDecisionInputSession()
        {
            Mode = LayoutDecisionMode.Research;
        }

        public AxisAlignedOrthogonalPolygon Room { get; private set; }

        public AxisAlignedOrthogonalPolygon OriginalRoom { get; private set; }

        public double TileWidth { get; private set; }

        public double TileHeight { get; private set; }

        public double GroutWidthMm { get; private set; }

        public double PlasterThicknessMm { get; private set; }

        public string FinishedFaceErrorMessage { get; private set; }

        public LayoutPolicyProfile Policy { get; private set; }

        public LayoutDecisionMode Mode { get; private set; }

        public bool PreferWallCornerAlignment { get; private set; }

        public OrthogonalBoundaryNormalizationResult BoundaryNormalization
        {
            get;
            private set;
        }

        public bool BoundaryWasNormalized { get; private set; }

        public double BoundaryPointMatchTolerance =>
            BoundaryWasNormalized && BoundaryNormalization != null
                ? BoundaryNormalization.PointMatchTolerance
                : GeometryTolerance.Coordinate;

        public AxisAlignedRectangle ControlRegion { get; private set; }

        public DoorOpening ControlDoor { get; private set; }

        public RoomLayoutIntent? LayoutIntent { get; private set; }

        public MainSecondaryRegionDefinition MainSecondary { get; private set; }

        public LineSegment3D? SelectedConnectionEdge { get; private set; }

        public DecisionRecord DecisionRecord { get; private set; }

        public EngineeringOrthogonalDecisionResult Result { get; private set; }

        public bool HasRoom => Room != null;

        public bool HasWriteAuthorization => false;

        public OrthogonalRoomValidationResult LoadBoundary(
            IReadOnlyCollection<LineSegment3D> boundaryLines,
            double tileWidth,
            double tileHeight,
            double groutWidthMm = 0.0,
            double plasterThicknessMm = 0.0)
        {
            ValidateTileDimension(tileWidth, nameof(tileWidth));
            ValidateTileDimension(tileHeight, nameof(tileHeight));
            ValidateNonNegativeFinite(
                groutWidthMm,
                nameof(groutWidthMm));
            ValidateNonNegativeFinite(
                plasterThicknessMm,
                nameof(plasterThicknessMm));

            GroutWidthMm = groutWidthMm;
            PlasterThicknessMm = plasterThicknessMm;

            OrthogonalRoomValidationResult validation =
                OrthogonalRoomValidator.Validate(boundaryLines);
            OrthogonalBoundaryNormalizationResult normalization =
                OrthogonalBoundaryNormalizer.Analyze(boundaryLines);
            ClearRoomInputs();
            BoundaryNormalization = normalization;
            if (!validation.IsValid)
            {
                if (validation.Error
                        == OrthogonalRoomValidationError.NonAxisAlignedLine
                    && normalization.Status
                        == OrthogonalBoundaryNormalizationStatus.NearOrthogonal)
                {
                    OrthogonalRoomValidationResult normalizedValidation =
                        OrthogonalRoomValidator.Validate(normalization.Lines);
                    if (normalizedValidation.IsValid)
                    {
                        validation = normalizedValidation;
                        BoundaryWasNormalized = true;
                    }
                    else
                    {
                        return normalizedValidation;
                    }
                }
                else
                {
                    return validation;
                }
            }

            OriginalRoom = validation.Room;
            TileWidth = tileWidth;
            TileHeight = tileHeight;
            if (!RefreshFinishedFace(null))
            {
                return OrthogonalRoomValidationResult.FinishedFaceFailure(
                    FinishedFaceErrorMessage);
            }

            Recalculate();
            return validation;
        }

        public void SetTileDimensions(
            double tileWidth,
            double tileHeight)
        {
            ValidateTileDimension(tileWidth, nameof(tileWidth));
            ValidateTileDimension(tileHeight, nameof(tileHeight));
            TileWidth = tileWidth;
            TileHeight = tileHeight;
            InvalidateDecisionRecord();
            RecalculateIfLoaded();
        }

        public void SetGroutWidth(double groutWidthMm)
        {
            ValidateNonNegativeFinite(
                groutWidthMm,
                nameof(groutWidthMm));
            if (GeometryTolerance.NearlyEqual(
                GroutWidthMm,
                groutWidthMm))
            {
                return;
            }

            GroutWidthMm = groutWidthMm;
            InvalidateDecisionRecord();
            RecalculateIfLoaded();
        }

        public void SetPlasterThickness(double plasterThicknessMm)
        {
            ValidateNonNegativeFinite(
                plasterThicknessMm,
                nameof(plasterThicknessMm));
            if (GeometryTolerance.NearlyEqual(
                PlasterThicknessMm,
                plasterThicknessMm))
            {
                return;
            }

            AxisAlignedOrthogonalPolygon oldRoom = Room;
            PlasterThicknessMm = plasterThicknessMm;
            if (OriginalRoom == null)
            {
                return;
            }

            if (!RefreshFinishedFace(oldRoom))
            {
                InvalidateDecisionRecord();
                return;
            }

            InvalidateDecisionRecord();
            RecalculateIfLoaded();
        }

        public void SetPolicy(
            LayoutPolicyProfile policy,
            LayoutDecisionMode mode)
        {
            if (!Enum.IsDefined(typeof(LayoutDecisionMode), mode))
            {
                throw new ArgumentOutOfRangeException(nameof(mode));
            }

            Policy = policy;
            Mode = mode;
            InvalidateDecisionRecord();
            RecalculateIfLoaded();
        }

        public void SetWallCornerAlignmentPreference(bool enabled)
        {
            if (PreferWallCornerAlignment == enabled)
            {
                return;
            }

            PreferWallCornerAlignment = enabled;
            InvalidateDecisionRecord();
            RecalculateIfLoaded();
        }

        public void SetControlRegion(AxisAlignedRectangle controlRegion)
        {
            ControlRegion = controlRegion;
            ControlDoor = null;
            if (MainSecondary != null
                && !SameRectangle(MainSecondary.MainRegion, controlRegion))
            {
                MainSecondary = null;
                SelectedConnectionEdge = null;
            }

            InvalidateDecisionRecord();
            RecalculateIfLoaded();
        }

        public void SetControlDoor(DoorOpening controlDoor)
        {
            ControlDoor = controlDoor;
            InvalidateDecisionRecord();
            RecalculateIfLoaded();
        }

        public void SetAutomaticDoorContext(
            AxisAlignedRectangle controlRegion,
            DoorOpening controlDoor)
        {
            if (controlRegion == null)
            {
                throw new ArgumentNullException(nameof(controlRegion));
            }

            if (controlDoor == null)
            {
                throw new ArgumentNullException(nameof(controlDoor));
            }

            ControlRegion = controlRegion;
            ControlDoor = controlDoor;
            LayoutIntent = RoomLayoutIntent.WholeRoomSinglePhase;
            MainSecondary = null;
            SelectedConnectionEdge = null;
            InvalidateDecisionRecord();
            RecalculateIfLoaded();
        }

        public void SetLayoutIntent(RoomLayoutIntent? layoutIntent)
        {
            if (layoutIntent.HasValue
                && !Enum.IsDefined(
                    typeof(RoomLayoutIntent),
                    layoutIntent.Value))
            {
                throw new ArgumentOutOfRangeException(nameof(layoutIntent));
            }

            LayoutIntent = layoutIntent;
            if (layoutIntent != RoomLayoutIntent.MainSecondary)
            {
                MainSecondary = null;
                SelectedConnectionEdge = null;
            }

            InvalidateDecisionRecord();
            RecalculateIfLoaded();
        }

        public void SetMainSecondary(
            MainSecondaryRegionDefinition mainSecondary)
        {
            MainSecondary = mainSecondary;
            SelectedConnectionEdge = null;
            LayoutIntent = RoomLayoutIntent.MainSecondary;
            if (mainSecondary != null)
            {
                bool changed = !SameRectangle(
                    ControlRegion,
                    mainSecondary.MainRegion);
                ControlRegion = mainSecondary.MainRegion;
                if (changed)
                {
                    ControlDoor = null;
                }
            }

            InvalidateDecisionRecord();
            RecalculateIfLoaded();
        }

        public void SetConnectionEdge(LineSegment3D? selectedConnectionEdge)
        {
            SelectedConnectionEdge = selectedConnectionEdge;
            InvalidateDecisionRecord();
            RecalculateIfLoaded();
        }

        public void ApplyDecisionRecord(DecisionRecord record)
        {
            if (record == null)
            {
                throw new ArgumentNullException(nameof(record));
            }

            EnsureRoomLoaded();
            DecisionRecord = record;
            Recalculate();
        }

        public void ClearDecisionRecord()
        {
            DecisionRecord = null;
            RecalculateIfLoaded();
        }

        public void ClearRoom()
        {
            ClearRoomInputs();
        }

        public EngineeringOrthogonalDecisionResult Recalculate()
        {
            EnsureRoomLoaded();
            var roomDecision = new RoomDecision(
                ControlRegion,
                ControlDoor,
                LayoutIntent,
                MainSecondary,
                SelectedConnectionEdge);
            Result = EngineeringOrthogonalDecisionCalculator.Calculate(
                new EngineeringOrthogonalDecisionRequest(
                    Room ?? OriginalRoom,
                    TileWidth,
                    TileHeight,
                    Policy,
                    roomDecision,
                    DecisionRecord == null
                        ? null
                        : new CandidateDecision(DecisionRecord),
                    Mode,
                    PreferWallCornerAlignment,
                    GroutWidthMm,
                    PlasterThicknessMm,
                    OriginalRoom));
            return Result;
        }

        public void Cancel()
        {
            Policy = null;
            Mode = LayoutDecisionMode.Research;
            PreferWallCornerAlignment = false;
            GroutWidthMm = 0.0;
            PlasterThicknessMm = 0.0;
            ClearRoomInputs();
        }

        private void RecalculateIfLoaded()
        {
            if (OriginalRoom != null)
            {
                Recalculate();
            }
        }

        private void InvalidateDecisionRecord()
        {
            DecisionRecord = null;
        }

        private void ClearRoomInputs()
        {
            Room = null;
            OriginalRoom = null;
            TileWidth = 0.0;
            TileHeight = 0.0;
            ControlRegion = null;
            ControlDoor = null;
            LayoutIntent = null;
            MainSecondary = null;
            SelectedConnectionEdge = null;
            DecisionRecord = null;
            Result = null;
            BoundaryNormalization = null;
            BoundaryWasNormalized = false;
            FinishedFaceErrorMessage = string.Empty;
        }

        private void EnsureRoomLoaded()
        {
            if (OriginalRoom == null)
            {
                throw new InvalidOperationException(
                    "A validated orthogonal room must be loaded first.");
            }
        }

        private static void ValidateTileDimension(double value, string name)
        {
            if (double.IsNaN(value)
                || double.IsInfinity(value)
                || value <= GeometryTolerance.Coordinate)
            {
                throw new ArgumentOutOfRangeException(
                    name,
                    "Tile dimensions must be finite and greater than the coordinate tolerance.");
            }
        }

        private static void ValidateNonNegativeFinite(
            double value,
            string name)
        {
            if (double.IsNaN(value)
                || double.IsInfinity(value)
                || value < 0.0)
            {
                throw new ArgumentOutOfRangeException(
                    name,
                    "The value must be a finite non-negative number.");
            }
        }

        private bool RefreshFinishedFace(
            AxisAlignedOrthogonalPolygon previousRoom)
        {
            FinishedFaceErrorMessage = string.Empty;
            OrthogonalRoomOffsetResult offset = OrthogonalRoomOffsetter.Offset(
                OriginalRoom,
                PlasterThicknessMm);
            if (!offset.IsValid)
            {
                Room = null;
                Result = null;
                ClearDependentRoomInputs();
                FinishedFaceErrorMessage = offset.ErrorMessage;
                return false;
            }

            AxisAlignedOrthogonalPolygon nextRoom = offset.Room;
            if (previousRoom != null)
            {
                ControlRegion = SyncRectangle(
                    ControlRegion,
                    previousRoom,
                    nextRoom);
                if (ControlRegion == null &&
                    (MainSecondary != null || ControlDoor != null))
                {
                    ClearDependentRoomInputs();
                }
                else
                {
                    ControlDoor = SyncDoor(
                        ControlDoor,
                        previousRoom,
                        nextRoom);
                    if (MainSecondary != null)
                    {
                        AxisAlignedRectangle main = SyncRectangle(
                            MainSecondary.MainRegion,
                            previousRoom,
                            nextRoom);
                        AxisAlignedRectangle secondary = SyncRectangle(
                            MainSecondary.SecondaryRegion,
                            previousRoom,
                            nextRoom);
                        MainSecondary = main == null || secondary == null
                            ? null
                            : new MainSecondaryRegionDefinition(
                                main,
                                secondary);
                    }

                    SelectedConnectionEdge = SyncEdge(
                        SelectedConnectionEdge,
                        previousRoom,
                        nextRoom);
                }
            }

            Room = nextRoom;
            return true;
        }

        private void ClearDependentRoomInputs()
        {
            ControlRegion = null;
            ControlDoor = null;
            MainSecondary = null;
            SelectedConnectionEdge = null;
            LayoutIntent = null;
        }

        private static AxisAlignedRectangle SyncRectangle(
            AxisAlignedRectangle rectangle,
            AxisAlignedOrthogonalPolygon oldRoom,
            AxisAlignedOrthogonalPolygon newRoom)
        {
            if (rectangle == null)
            {
                return null;
            }

            double west = SyncBoundaryCoordinate(
                rectangle.West,
                true,
                rectangle.South,
                rectangle.North,
                oldRoom,
                newRoom);
            double east = SyncBoundaryCoordinate(
                rectangle.East,
                true,
                rectangle.South,
                rectangle.North,
                oldRoom,
                newRoom);
            double south = SyncBoundaryCoordinate(
                rectangle.South,
                false,
                rectangle.West,
                rectangle.East,
                oldRoom,
                newRoom);
            double north = SyncBoundaryCoordinate(
                rectangle.North,
                false,
                rectangle.West,
                rectangle.East,
                oldRoom,
                newRoom);
            try
            {
                return new AxisAlignedRectangle(
                    west,
                    east,
                    south,
                    north,
                    newRoom.Elevation);
            }
            catch (ArgumentOutOfRangeException)
            {
                return null;
            }
        }

        private static DoorOpening SyncDoor(
            DoorOpening door,
            AxisAlignedOrthogonalPolygon oldRoom,
            AxisAlignedOrthogonalPolygon newRoom)
        {
            if (door == null)
            {
                return null;
            }

            bool vertical = door.Wall == RoomSide.West
                || door.Wall == RoomSide.East;
            double oldLow = vertical ? oldRoom.South : oldRoom.West;
            double oldHigh = vertical ? oldRoom.North : oldRoom.East;
            double newLow = vertical ? newRoom.South : newRoom.West;
            double newHigh = vertical ? newRoom.North : newRoom.East;
            double start = door.AlongWallStart;
            double end = door.AlongWallEnd;
            BoundaryEdge oldEdge;
            BoundaryEdge newEdge;
            bool hasMatchingEdge = TryFindCorrespondingBoundaryEdge(
                oldRoom,
                newRoom,
                vertical,
                vertical
                    ? (door.Wall == RoomSide.West
                        ? oldRoom.West
                        : oldRoom.East)
                    : (door.Wall == RoomSide.South
                        ? oldRoom.South
                        : oldRoom.North),
                start,
                end,
                out oldEdge,
                out newEdge);
            if (hasMatchingEdge)
            {
                if (start < oldEdge.Minimum - GeometryTolerance.Coordinate
                    || end > oldEdge.Maximum + GeometryTolerance.Coordinate)
                {
                    return null;
                }

                start = SyncEdgeEndpoint(
                    start,
                    oldEdge,
                    newEdge);
                end = SyncEdgeEndpoint(
                    end,
                    oldEdge,
                    newEdge);
                if (start < newEdge.Minimum - GeometryTolerance.Coordinate
                    || end > newEdge.Maximum + GeometryTolerance.Coordinate)
                {
                    return null;
                }
            }
            else
            {
                start = SyncBoundaryCoordinate(
                    start,
                    oldLow,
                    newLow);
                end = SyncBoundaryCoordinate(
                    end,
                    oldHigh,
                    newHigh);
            }
            try
            {
                return new DoorOpening(door.Wall, start, end);
            }
            catch (ArgumentOutOfRangeException)
            {
                return null;
            }
        }

        private static LineSegment3D? SyncEdge(
            LineSegment3D? edge,
            AxisAlignedOrthogonalPolygon oldRoom,
            AxisAlignedOrthogonalPolygon newRoom)
        {
            if (!edge.HasValue)
            {
                return null;
            }

            LineSegment3D value = edge.Value;
            double deltaX = value.End.X - value.Start.X;
            double deltaY = value.End.Y - value.Start.Y;
            if (Math.Abs(deltaX) <= GeometryTolerance.Coordinate
                && Math.Abs(deltaY) <= GeometryTolerance.Coordinate)
            {
                return null;
            }

            if (Math.Abs(deltaX) > GeometryTolerance.Coordinate
                && Math.Abs(deltaY) > GeometryTolerance.Coordinate)
            {
                return null;
            }

            bool vertical = Math.Abs(deltaX)
                <= GeometryTolerance.Coordinate;
            Point3D mappedStart;
            Point3D mappedEnd;
            bool startMapped = TryMapBoundaryPoint(
                value.Start,
                oldRoom,
                newRoom,
                out mappedStart);
            bool endMapped = TryMapBoundaryPoint(
                value.End,
                oldRoom,
                newRoom,
                out mappedEnd);
            double fixedCoordinate = vertical ? value.Start.X : value.Start.Y;
            if (vertical && startMapped && endMapped)
            {
                if (!GeometryTolerance.NearlyEqual(
                    mappedStart.X,
                    mappedEnd.X))
                {
                    return null;
                }
                fixedCoordinate = mappedStart.X;
            }
            else if (vertical && startMapped)
            {
                fixedCoordinate = mappedStart.X;
            }
            else if (vertical && endMapped)
            {
                fixedCoordinate = mappedEnd.X;
            }
            else if (!vertical && startMapped && endMapped)
            {
                if (!GeometryTolerance.NearlyEqual(
                    mappedStart.Y,
                    mappedEnd.Y))
                {
                    return null;
                }
                fixedCoordinate = mappedStart.Y;
            }
            else if (!vertical && startMapped)
            {
                fixedCoordinate = mappedStart.Y;
            }
            else if (!vertical && endMapped)
            {
                fixedCoordinate = mappedEnd.Y;
            }

            double startAlong = vertical ? value.Start.Y : value.Start.X;
            double endAlong = vertical ? value.End.Y : value.End.X;
            if (startMapped)
            {
                startAlong = vertical ? mappedStart.Y : mappedStart.X;
            }

            if (endMapped)
            {
                endAlong = vertical ? mappedEnd.Y : mappedEnd.X;
            }

            return vertical
                ? new LineSegment3D(
                    new Point3D(fixedCoordinate, startAlong, value.Start.Z),
                    new Point3D(fixedCoordinate, endAlong, value.End.Z))
                : new LineSegment3D(
                    new Point3D(startAlong, fixedCoordinate, value.Start.Z),
                    new Point3D(endAlong, fixedCoordinate, value.End.Z));
        }

        private static double SyncBoundaryCoordinate(
            double value,
            bool vertical,
            double alongStart,
            double alongEnd,
            AxisAlignedOrthogonalPolygon oldRoom,
            AxisAlignedOrthogonalPolygon newRoom)
        {
            BoundaryEdge oldEdge;
            BoundaryEdge newEdge;
            if (TryFindCorrespondingBoundaryEdge(
                oldRoom,
                newRoom,
                vertical,
                value,
                alongStart,
                alongEnd,
                out oldEdge,
                out newEdge))
            {
                return newEdge.FixedCoordinate;
            }

            if (vertical)
            {
                if (GeometryTolerance.NearlyEqual(value, oldRoom.West))
                {
                    return newRoom.West;
                }

                if (GeometryTolerance.NearlyEqual(value, oldRoom.East))
                {
                    return newRoom.East;
                }
            }
            else
            {
                if (GeometryTolerance.NearlyEqual(value, oldRoom.South))
                {
                    return newRoom.South;
                }

                if (GeometryTolerance.NearlyEqual(value, oldRoom.North))
                {
                    return newRoom.North;
                }
            }

            return value;
        }

        private static double SyncBoundaryCoordinate(
            double value,
            double oldBoundary,
            double newBoundary)
        {
            return GeometryTolerance.NearlyEqual(value, oldBoundary)
                ? newBoundary
                : value;
        }

        private static double SyncEdgeEndpoint(
            double value,
            BoundaryEdge oldEdge,
            BoundaryEdge newEdge)
        {
            if (GeometryTolerance.NearlyEqual(value, oldEdge.Minimum))
            {
                return newEdge.Minimum;
            }

            if (GeometryTolerance.NearlyEqual(value, oldEdge.Maximum))
            {
                return newEdge.Maximum;
            }

            return value;
        }

        private static bool TryFindCorrespondingBoundaryEdge(
            AxisAlignedOrthogonalPolygon oldRoom,
            AxisAlignedOrthogonalPolygon newRoom,
            bool vertical,
            double fixedCoordinate,
            double alongStart,
            double alongEnd,
            out BoundaryEdge oldEdge,
            out BoundaryEdge newEdge)
        {
            oldEdge = default(BoundaryEdge);
            newEdge = default(BoundaryEdge);
            if (oldRoom == null
                || newRoom == null
                || oldRoom.Vertices == null
                || newRoom.Vertices == null
                || oldRoom.Vertices.Count != newRoom.Vertices.Count)
            {
                return false;
            }

            double minimum = Math.Min(alongStart, alongEnd);
            double maximum = Math.Max(alongStart, alongEnd);
            int bestIndex = -1;
            double bestOverlap = 0.0;
            for (int index = 0; index < oldRoom.Vertices.Count; index++)
            {
                BoundaryEdge candidate;
                if (!TryGetBoundaryEdge(oldRoom, index, out candidate)
                    || candidate.IsVertical != vertical
                    || !GeometryTolerance.NearlyEqual(
                        candidate.FixedCoordinate,
                        fixedCoordinate))
                {
                    continue;
                }

                double overlap = Math.Min(candidate.Maximum, maximum)
                    - Math.Max(candidate.Minimum, minimum);
                if (overlap > bestOverlap + GeometryTolerance.Coordinate)
                {
                    bestIndex = index;
                    bestOverlap = overlap;
                    oldEdge = candidate;
                }
            }

            if (bestIndex < 0
                || bestOverlap <= GeometryTolerance.Coordinate
                || !TryGetBoundaryEdge(newRoom, bestIndex, out newEdge)
                || newEdge.IsVertical != vertical)
            {
                oldEdge = default(BoundaryEdge);
                newEdge = default(BoundaryEdge);
                return false;
            }

            return true;
        }

        private static bool TryMapBoundaryPoint(
            Point3D point,
            AxisAlignedOrthogonalPolygon oldRoom,
            AxisAlignedOrthogonalPolygon newRoom,
            out Point3D mapped)
        {
            mapped = point;
            if (oldRoom == null
                || newRoom == null
                || oldRoom.Vertices == null
                || newRoom.Vertices == null
                || oldRoom.Vertices.Count != newRoom.Vertices.Count)
            {
                return false;
            }

            for (int index = 0; index < oldRoom.Vertices.Count; index++)
            {
                Point3D oldVertex = oldRoom.Vertices[index];
                if (!GeometryTolerance.NearlyEqual(oldVertex.X, point.X)
                    || !GeometryTolerance.NearlyEqual(oldVertex.Y, point.Y))
                {
                    continue;
                }

                Point3D newVertex = newRoom.Vertices[index];
                mapped = new Point3D(newVertex.X, newVertex.Y, point.Z);
                return true;
            }

            for (int index = 0; index < oldRoom.Vertices.Count; index++)
            {
                BoundaryEdge oldEdge;
                BoundaryEdge newEdge;
                if (!TryGetBoundaryEdge(oldRoom, index, out oldEdge)
                    || !TryGetBoundaryEdge(newRoom, index, out newEdge))
                {
                    continue;
                }

                double along = oldEdge.IsVertical ? point.Y : point.X;
                if (!GeometryTolerance.NearlyEqual(
                        oldEdge.FixedCoordinate,
                        oldEdge.IsVertical ? point.X : point.Y)
                    || along < oldEdge.Minimum - GeometryTolerance.Coordinate
                    || along > oldEdge.Maximum + GeometryTolerance.Coordinate)
                {
                    continue;
                }

                double mappedAlong = SyncEdgeEndpoint(
                    along,
                    oldEdge,
                    newEdge);
                mapped = oldEdge.IsVertical
                    ? new Point3D(newEdge.FixedCoordinate, mappedAlong, point.Z)
                    : new Point3D(mappedAlong, newEdge.FixedCoordinate, point.Z);
                return true;
            }

            return false;
        }

        private static bool TryGetBoundaryEdge(
            AxisAlignedOrthogonalPolygon room,
            int index,
            out BoundaryEdge edge)
        {
            edge = default(BoundaryEdge);
            if (room == null
                || room.Vertices == null
                || index < 0
                || index >= room.Vertices.Count)
            {
                return false;
            }

            Point3D start = room.Vertices[index];
            Point3D end = room.Vertices[(index + 1) % room.Vertices.Count];
            double deltaX = end.X - start.X;
            double deltaY = end.Y - start.Y;
            if (Math.Abs(deltaX) <= GeometryTolerance.Coordinate
                && Math.Abs(deltaY) <= GeometryTolerance.Coordinate)
            {
                return false;
            }

            if (Math.Abs(deltaX) <= GeometryTolerance.Coordinate)
            {
                edge = new BoundaryEdge(
                    true,
                    (start.X + end.X) / 2.0,
                    Math.Min(start.Y, end.Y),
                    Math.Max(start.Y, end.Y));
                return true;
            }

            if (Math.Abs(deltaY) <= GeometryTolerance.Coordinate)
            {
                edge = new BoundaryEdge(
                    false,
                    (start.Y + end.Y) / 2.0,
                    Math.Min(start.X, end.X),
                    Math.Max(start.X, end.X));
                return true;
            }

            return false;
        }

        private struct BoundaryEdge
        {
            public BoundaryEdge(
                bool isVertical,
                double fixedCoordinate,
                double minimum,
                double maximum)
            {
                IsVertical = isVertical;
                FixedCoordinate = fixedCoordinate;
                Minimum = minimum;
                Maximum = maximum;
            }

            public bool IsVertical { get; }

            public double FixedCoordinate { get; }

            public double Minimum { get; }

            public double Maximum { get; }
        }

        private static bool SameRectangle(
            AxisAlignedRectangle first,
            AxisAlignedRectangle second)
        {
            if (first == null || second == null)
            {
                return first == second;
            }

            return GeometryTolerance.NearlyEqual(first.West, second.West)
                && GeometryTolerance.NearlyEqual(first.East, second.East)
                && GeometryTolerance.NearlyEqual(first.South, second.South)
                && GeometryTolerance.NearlyEqual(first.North, second.North)
                && GeometryTolerance.NearlyEqual(
                    first.Elevation,
                    second.Elevation);
        }
    }
}
