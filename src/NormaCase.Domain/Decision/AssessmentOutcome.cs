namespace NormaCase.Domain.Decision;

/// <summary>
/// Platform-level outcomes. Knowledge Packs may attach domain-specific
/// display text, but the core does not encode medical decisions.
/// </summary>
public enum AssessmentOutcome
{
    Incomplete = 0,
    HumanReview = 1,
    Supported = 2,
    NotSupported = 3,
    NotApplicable = 4
}
