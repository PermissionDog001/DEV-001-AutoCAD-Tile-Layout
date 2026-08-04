using System;

namespace TileLayout.Core
{
    public sealed class DecisionRecord
    {
        public DecisionRecord(
            string candidateId,
            string policyVersion,
            string reason,
            bool acceptsException = false)
        {
            if (string.IsNullOrWhiteSpace(candidateId))
            {
                throw new ArgumentException("A candidate id is required.", nameof(candidateId));
            }

            if (string.IsNullOrWhiteSpace(policyVersion))
            {
                throw new ArgumentException("A policy version is required.", nameof(policyVersion));
            }

            if (acceptsException && string.IsNullOrWhiteSpace(reason))
            {
                throw new ArgumentException(
                    "An exception acceptance reason is required.",
                    nameof(reason));
            }

            CandidateId = candidateId;
            PolicyVersion = policyVersion;
            Reason = reason ?? string.Empty;
            AcceptsException = acceptsException;
        }

        public string CandidateId { get; }

        public string PolicyVersion { get; }

        public string Reason { get; }

        public bool AcceptsException { get; }
    }
}
