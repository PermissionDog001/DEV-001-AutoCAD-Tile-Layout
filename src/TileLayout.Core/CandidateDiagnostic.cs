namespace TileLayout.Core
{
    public sealed class CandidateDiagnostic
    {
        internal CandidateDiagnostic(
            CandidateDiagnosticCode code,
            CandidateDiagnosticSeverity severity,
            string message,
            TileLayoutAxis? axis = null,
            RoomSide? side = null,
            double? actualValue = null,
            double? threshold = null)
        {
            Code = code;
            Severity = severity;
            Message = message;
            Axis = axis;
            Side = side;
            ActualValue = actualValue;
            Threshold = threshold;
        }

        public CandidateDiagnosticCode Code { get; }

        public CandidateDiagnosticSeverity Severity { get; }

        public string Message { get; }

        public TileLayoutAxis? Axis { get; }

        public RoomSide? Side { get; }

        public double? ActualValue { get; }

        public double? Threshold { get; }
    }
}
