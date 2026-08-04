using System;
using System.Globalization;

namespace TileLayout.Core
{
    public sealed class TileLayoutLimitExceededException : InvalidOperationException
    {
        internal TileLayoutLimitExceededException(
            double estimatedDivisionLineCount,
            int maximumDivisionLineCount)
            : base(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "The estimated division line count ({0}) exceeds the maximum ({1}).",
                    estimatedDivisionLineCount,
                    maximumDivisionLineCount))
        {
            EstimatedDivisionLineCount = estimatedDivisionLineCount;
            MaximumDivisionLineCount = maximumDivisionLineCount;
        }

        public double EstimatedDivisionLineCount { get; }

        public int MaximumDivisionLineCount { get; }
    }
}
