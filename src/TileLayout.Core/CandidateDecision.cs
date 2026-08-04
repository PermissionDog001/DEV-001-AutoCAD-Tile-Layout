namespace TileLayout.Core
{
    public sealed class CandidateDecision
    {
        public CandidateDecision(DecisionRecord record = null)
        {
            Record = record;
        }

        public DecisionRecord Record { get; }
    }
}
