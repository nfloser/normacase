namespace NormaCase.Domain.Decision;

/// <summary>
/// Platform-level outcomes. Knowledge Packs may attach domain-specific
/// display text, but the core does not encode medical decisions.
/// </summary>
public enum AssessmentOutcome
{
    Supported = 0,
    NotSupported = 1,
    Incomplete = 2,
    HumanReview = 3,
    NotApplicable = 4
}
