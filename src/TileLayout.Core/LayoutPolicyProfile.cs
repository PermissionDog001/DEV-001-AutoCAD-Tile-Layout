using System;

namespace TileLayout.Core
{
    public sealed class LayoutPolicyProfile
    {
        public LayoutPolicyProfile(
            string version,
            double? projectAbsoluteMinimumCut = null,
            ProjectAbsoluteMinimumMode projectAbsoluteMinimumMode =
                ProjectAbsoluteMinimumMode.NotDecided,
            double recommendedMinimumCutRatio =
                EngineeringLayoutRules.DefaultMinimumCutRatio,
            double? projectAbsoluteMinimumRatio = null)
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
                && projectAbsoluteMinimumRatio.HasValue)
            {
                throw new ArgumentException(
                    "A project absolute minimum must be expressed either as a millimetre value or as a tile-size ratio, not both.",
                    nameof(projectAbsoluteMinimumRatio));
            }

            if ((projectAbsoluteMinimumCut.HasValue
                    || projectAbsoluteMinimumRatio.HasValue)
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

            if (!projectAbsoluteMinimumRatio.HasValue
                && projectAbsoluteMinimumMode
                    == ProjectAbsoluteMinimumMode.NumericRatio)
            {
                throw new ArgumentException(
                    "Numeric-ratio project absolute minimum mode requires a value.",
                    nameof(projectAbsoluteMinimumRatio));
            }

            if (projectAbsoluteMinimumMode == ProjectAbsoluteMinimumMode.Numeric
                && projectAbsoluteMinimumRatio.HasValue)
            {
                throw new ArgumentException(
                    "Numeric project absolute minimum mode cannot carry a ratio value.",
                    nameof(projectAbsoluteMinimumRatio));
            }

            if (projectAbsoluteMinimumMode
                    == ProjectAbsoluteMinimumMode.NumericRatio
                && projectAbsoluteMinimumCut.HasValue)
            {
                throw new ArgumentException(
                    "Numeric-ratio project absolute minimum mode cannot carry a millimetre value.",
                    nameof(projectAbsoluteMinimumCut));
            }

            if (projectAbsoluteMinimumCut.HasValue
                && projectAbsoluteMinimumMode
                    == ProjectAbsoluteMinimumMode.NotDecided)
            {
                projectAbsoluteMinimumMode = ProjectAbsoluteMinimumMode.Numeric;
            }

            if (projectAbsoluteMinimumRatio.HasValue
                && projectAbsoluteMinimumMode
                    == ProjectAbsoluteMinimumMode.NotDecided)
            {
                projectAbsoluteMinimumMode =
                    ProjectAbsoluteMinimumMode.NumericRatio;
            }

            if (projectAbsoluteMinimumCut.HasValue
                && (!IsFinite(projectAbsoluteMinimumCut.Value)
                    || projectAbsoluteMinimumCut.Value <= GeometryTolerance.Coordinate))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(projectAbsoluteMinimumCut),
                    "The project absolute minimum must be finite and positive when it is set.");
            }

            EngineeringLayoutRules.ValidateMinimumCutRatio(
                recommendedMinimumCutRatio,
                nameof(recommendedMinimumCutRatio));

            if (projectAbsoluteMinimumRatio.HasValue)
            {
                EngineeringLayoutRules.ValidateMinimumCutRatio(
                    projectAbsoluteMinimumRatio.Value,
                    nameof(projectAbsoluteMinimumRatio));
                if (projectAbsoluteMinimumRatio.Value
                    > recommendedMinimumCutRatio + GeometryTolerance.Coordinate)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(projectAbsoluteMinimumRatio),
                        "The project absolute minimum ratio cannot exceed the recommended minimum ratio.");
                }
            }

            Version = version;
            DefaultMinimumCutRatio = recommendedMinimumCutRatio;
            ProjectAbsoluteMinimumCut = projectAbsoluteMinimumCut;
            ProjectAbsoluteMinimumRatio = projectAbsoluteMinimumRatio;
            ProjectAbsoluteMinimumMode = projectAbsoluteMinimumMode;
        }

        public string Version { get; }

        public double DefaultMinimumCutRatio { get; }

        public double? ProjectAbsoluteMinimumCut { get; }

        public double? ProjectAbsoluteMinimumRatio { get; }

        public ProjectAbsoluteMinimumMode ProjectAbsoluteMinimumMode { get; }

        public bool AllowsVisualConfirmation =>
            ProjectAbsoluteMinimumMode
                == global::TileLayout.Core.ProjectAbsoluteMinimumMode.VisualConfirmation;

        public bool IsProjectAbsoluteMinimumDecisionMade =>
            ProjectAbsoluteMinimumMode
                != global::TileLayout.Core.ProjectAbsoluteMinimumMode.NotDecided;

        public bool HasProjectAbsoluteMinimum =>
            ProjectAbsoluteMinimumCut.HasValue
            || ProjectAbsoluteMinimumRatio.HasValue;

        public double? SecondAbsoluteMinimumCut => ProjectAbsoluteMinimumCut;

        public double? SecondAbsoluteMinimumRatio => ProjectAbsoluteMinimumRatio;

        public bool HasSecondAbsoluteMinimum => HasProjectAbsoluteMinimum;

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }
}
