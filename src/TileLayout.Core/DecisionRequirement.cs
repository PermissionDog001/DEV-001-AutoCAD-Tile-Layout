using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace TileLayout.Core
{
    public sealed class DecisionRequirement
    {
        public DecisionRequirement(
            DecisionRequirementCode code,
            DecisionRequirementLevel level,
            string reason,
            string requiredInput,
            IList<string> options = null,
            IList<string> affectedCandidateIds = null)
        {
            if (!Enum.IsDefined(typeof(DecisionRequirementCode), code))
            {
                throw new ArgumentOutOfRangeException(nameof(code));
            }

            if (!Enum.IsDefined(typeof(DecisionRequirementLevel), level))
            {
                throw new ArgumentOutOfRangeException(nameof(level));
            }

            if (string.IsNullOrWhiteSpace(reason))
            {
                throw new ArgumentException("A decision reason is required.", nameof(reason));
            }

            Code = code;
            Level = level;
            Reason = reason;
            RequiredInput = requiredInput ?? string.Empty;
            Options = new ReadOnlyCollection<string>(options ?? new List<string>());
            AffectedCandidateIds = new ReadOnlyCollection<string>(
                affectedCandidateIds ?? new List<string>());
        }

        public DecisionRequirementCode Code { get; }

        public DecisionRequirementLevel Level { get; }

        public string Reason { get; }

        public string RequiredInput { get; }

        public IReadOnlyList<string> Options { get; }

        public IReadOnlyList<string> AffectedCandidateIds { get; }
    }
}
