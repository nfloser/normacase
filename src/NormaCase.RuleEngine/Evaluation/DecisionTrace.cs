using NormaCase.Domain.Cases;
using NormaCase.Domain.Decision;
using NormaCase.Domain.Evidence;

namespace NormaCase.RuleEngine.Evaluation;

public sealed record ConditionTrace(
    string Kind,
    ConditionResult Result,
    string? Field,
    CaseValue? Expected,
    CaseValue? Actual,
    decimal? Minimum,
    decimal? Maximum,
    IReadOnlyList<ConditionTrace> Children,
    string? EvidenceRequirementId = null,
    EvidenceStatus? EvidenceStatus = null);

public sealed record RuleTrace(
    string RuleId,
    int RuleVersion,
    string SourceId,
    ConditionResult ConditionResult,
    AssessmentOutcome Outcome,
    ConditionTrace Condition,
    SourceTrace Source);

/// <summary>Detached immutable metadata for the exact source revision used by this rule.</summary>
public sealed record SourceTrace(
    string Id,
    string? Version,
    string? SourceLocation,
    string Authority,
    string Title,
    string DocumentType,
    string Status,
    DateOnly? PublicationDate,
    DateOnly? ValidFrom,
    DateOnly? ValidUntil,
    DateOnly? RetrievedAt,
    string? ContentHash);
