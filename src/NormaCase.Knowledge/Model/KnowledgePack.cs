using NormaCase.Domain.Decision;

namespace NormaCase.Knowledge.Model;

public sealed class KnowledgePack
{
    public KnowledgeManifest Manifest { get; init; } = new();
    public List<FieldDefinition> Fields { get; init; } = [];
    public List<CalculationDefinition> Calculations { get; init; } = [];
    public List<EvidenceRequirementDefinition> EvidenceRequirements { get; init; } = [];
    public List<SourceDefinition> Sources { get; init; } = [];
    public List<RuleDefinition> Rules { get; init; } = [];
}

public sealed class KnowledgeManifest
{
    public int FormatVersion { get; init; }
    public string PackId { get; init; } = string.Empty;
    public string ReleaseId { get; init; } = string.Empty;
    public string LifecycleStatus { get; init; } = string.Empty;
    public string ValidationLevel { get; init; } = string.Empty;
    public string EntryRuleId { get; init; } = string.Empty;
}

public sealed class FieldDefinition
{
    public string Id { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public bool Required { get; init; }
}

public sealed class CalculationDefinition
{
    public string Id { get; init; } = string.Empty;
    public string Kind { get; init; } = string.Empty;
    public string? Input { get; init; }
    public List<string> Inputs { get; init; } = [];
    public bool RequireFullCoverage { get; init; }
    public decimal? CoverageMinimum { get; init; }
    public decimal? CoverageMaximum { get; init; }
    public List<RangeLookupDefinition> Ranges { get; init; } = [];
}

public sealed class RangeLookupDefinition
{
    public decimal? Minimum { get; init; }
    public bool MinimumInclusive { get; init; } = true;
    public decimal? Maximum { get; init; }
    public bool MaximumInclusive { get; init; } = true;
    public decimal? Value { get; init; }
}

public sealed class EvidenceRequirementDefinition
{
    public string Id { get; init; } = string.Empty;
}

public sealed class SourceDefinition
{
    public string Id { get; init; } = string.Empty;
    public string Authority { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string DocumentType { get; init; } = string.Empty;
    public DateOnly? PublicationDate { get; init; }
    public DateOnly? ValidFrom { get; init; }
    public DateOnly? ValidUntil { get; init; }
    public string? Version { get; init; }
    public string? SourceLocation { get; init; }
    public DateOnly? RetrievedAt { get; init; }
    public string? ContentHash { get; init; }
    public string Status { get; init; } = string.Empty;
}

public sealed class RuleDefinition
{
    public string Id { get; init; } = string.Empty;
    public int Version { get; init; }
    public DateOnly ValidFrom { get; init; }
    public DateOnly? ValidUntil { get; init; }
    public string SourceId { get; init; } = string.Empty;
    public ConditionDefinition Condition { get; init; } = new();
    public AssessmentOutcome? OnMatch { get; init; }
    public AssessmentOutcome? OnNoMatch { get; init; }
    public AssessmentOutcome? OnUnknown { get; init; }
}

public sealed class ConditionDefinition
{
    public string Kind { get; init; } = string.Empty;
    public string? Field { get; init; }
    public string? EvidenceRequirementId { get; init; }
    public TruthValue? Expected { get; init; }
    public decimal? Threshold { get; init; }
    public decimal? Minimum { get; init; }
    public decimal? Maximum { get; init; }
    public List<ConditionDefinition> Conditions { get; init; } = [];
}
