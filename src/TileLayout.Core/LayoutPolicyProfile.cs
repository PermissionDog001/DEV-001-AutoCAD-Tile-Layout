using System;

namespace TileLayout.Core
{
    public sealed class LayoutPolicyProfile
    {
        public LayoutPolicyProfile(
            string version,
            double? projectAbsoluteMinimumCut = null,
            ProjectAbsoluteMinimumMode projectAbsoluteMinimumMode =
                ProjectAbsoluteMinimumMode.NotDecided)
        {
            if (string.IsNullOrWhiteSpace(version))
            {
                throw new ArgumentException("A policy version is required.", nameof(version));
            }

            if (!Enum.IsDefined(
                typeof(ProjectAbsoluteMinimumMode),
                projectAbsoluteMinimumMode))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(projectAbsoluteMinimumMode));
            }

            if (projectAbsoluteMinimumCut.HasValue
                && projectAbsoluteMinimumMode
                    == ProjectAbsoluteMinimumMode.VisualConfirmation)
            {
                throw new ArgumentException(
                    "Visual confirmation mode cannot carry a numeric project absolute minimum.",
                    nameof(projectAbsoluteMinimumCut));
            }

            if (!projectAbsoluteMinimumCut.HasValue
                && projectAbsoluteMinimumMode == ProjectAbsoluteMinimumMode.Numeric)
            {
                throw new ArgumentException(
                    "Numeric project absolute minimum mode requires a value.",
                    nameof(projectAbsoluteMinimumCut));
            }

            if (projectAbsoluteMinimumCut.HasValue
                && projectAbsoluteMinimumMode
                    == ProjectAbsoluteMinimumMode.NotDecided)
            {
                projectAbsoluteMinimumMode = ProjectAbsoluteMinimumMode.Numeric;
            }

            if (projectAbsoluteMinimumCut.HasValue
                && (!IsFinite(projectAbsoluteMinimumCut.Value)
                    || projectAbsoluteMinimumCut.Value <= GeometryTolerance.Coordinate))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(projectAbsoluteMinimumCut),
                    "The project absolute minimum must be finite and positive when it is set.");
            }

            Version = version;
            DefaultMinimumCutRatio = EngineeringLayoutRules.DefaultMinimumCutRatio;
            ProjectAbsoluteMinimumCut = projectAbsoluteMinimumCut;
            ProjectAbsoluteMinimumMode = projectAbsoluteMinimumMode;
        }

        public string Version { get; }

        public double DefaultMinimumCutRatio { get; }

        public double? ProjectAbsoluteMinimumCut { get; }

        public ProjectAbsoluteMinimumMode ProjectAbsoluteMinimumMode { get; }

        public bool AllowsVisualConfirmation =>
            ProjectAbsoluteMinimumMode
                == global::TileLayout.Core.ProjectAbsoluteMinimumMode.VisualConfirmation;

        public bool IsProjectAbsoluteMinimumDecisionMade =>
            ProjectAbsoluteMinimumMode
                != global::TileLayout.Core.ProjectAbsoluteMinimumMode.NotDecided;

        public bool HasProjectAbsoluteMinimum => ProjectAbsoluteMinimumCut.HasValue;

        public double? SecondAbsoluteMinimumCut => ProjectAbsoluteMinimumCut;

        public bool HasSecondAbsoluteMinimum => HasProjectAbsoluteMinimum;

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }
}
