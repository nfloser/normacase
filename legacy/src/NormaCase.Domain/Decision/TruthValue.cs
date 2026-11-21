namespace NormaCase.Domain.Decision;

/// <summary>
/// Four-state value used by deterministic criteria evaluation.
/// UNKNOWN must never be coerced to YES or NO.
/// </summary>
public enum TruthValue
{
    Unknown = 0,
    Yes = 1,
    No = 2,
    NotApplicable = 3
}
