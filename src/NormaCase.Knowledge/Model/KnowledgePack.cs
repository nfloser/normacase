using NormaCase.Domain.Decision;

namespace NormaCase.Knowledge.Model;

public sealed class KnowledgePack
{
    public KnowledgeManifest Manifest { get; init; } = new();
    public List<FieldDefinition> Fields { get; init; } = [];
    public List<SourceDefinition> Sources { get; init; } = [];
    public List<RuleDefinition> Rules { get; init; } = [];
}

public sealed class KnowledgeManifest
{
    public string PackId { get; init; } = string.Empty;
    public string ReleaseId { get; init; } = string.Empty;
    public string ValidationLevel { get; init; } = string.Empty;
    public string EntryRuleId { get; init; } = string.Empty;
}

public sealed class FieldDefinition
{
    public string Id { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public bool Required { get; init; }
}

public sealed class SourceDefinition
{
    public string Id { get; init; } = string.Empty;
    public string Authority { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string DocumentType { get; init; } = string.Empty;
}

public sealed class RuleDefinition
{
    public string Id { get; init; } = string.Empty;
    public int Version { get; init; }
    public DateOnly ValidFrom { get; init; }
    public DateOnly? ValidUntil { get; init; }
    public string SourceId { get; init; } = string.Empty;
    public ConditionDefinition Condition { get; init; } = new();
    public AssessmentOutcome OnMatch { get; init; }
    public AssessmentOutcome OnNoMatch { get; init; }
}

public sealed class ConditionDefinition
{
    public string Kind { get; init; } = string.Empty;
    public string? Field { get; init; }
    public TruthValue? Expected { get; init; }
    public List<ConditionDefinition> Conditions { get; init; } = [];
}
