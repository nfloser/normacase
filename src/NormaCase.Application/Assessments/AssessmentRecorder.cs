using NormaCase.Domain.Cases;
using NormaCase.Domain.Evidence;
using NormaCase.Knowledge.Model;
using NormaCase.RuleEngine.Evaluation;

namespace NormaCase.Application.Assessments;

public sealed class AssessmentRecorder
{
    private readonly RuleEvaluator _evaluator;

    public AssessmentRecorder(RuleEvaluator? evaluator = null)
    {
        _evaluator = evaluator ?? new RuleEvaluator();
    }

    public AssessmentRecord Evaluate(
        KnowledgePack pack,
        IReadOnlyDictionary<string, CaseValue> facts,
        DateOnly assessmentDate,
        IReadOnlyDictionary<string, EvidenceStatus>? evidence,
        AssessmentExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(pack);
        ArgumentNullException.ThrowIfNull(facts);
        ArgumentNullException.ThrowIfNull(context);

        var suppliedFacts = facts.ToDictionary(
            item => item.Key,
            item => item.Value,
            StringComparer.Ordinal);
        var suppliedEvidence = evidence?.ToDictionary(
            item => item.Key,
            item => item.Value,
            StringComparer.Ordinal)
            ?? new Dictionary<string, EvidenceStatus>(StringComparer.Ordinal);

        var result = _evaluator.Evaluate(
            pack,
            suppliedFacts,
            assessmentDate,
            suppliedEvidence);

        var snapshotFacts = pack.Fields.ToDictionary(
            field => field.Id,
            field => suppliedFacts.TryGetValue(field.Id, out var value)
                ? value
                : CaseValue.Unknown,
            StringComparer.Ordinal);
        var snapshotEvidence = pack.EvidenceRequirements.ToDictionary(
            requirement => requirement.Id,
            requirement => suppliedEvidence.TryGetValue(requirement.Id, out var status)
                ? status
                : EvidenceStatus.Missing,
            StringComparer.Ordinal);

        var input = new AssessmentInputSnapshot(
            assessmentDate,
            snapshotFacts,
            snapshotEvidence);

        return new AssessmentRecord(
            context.AssessmentId,
            context.CaseId,
            pack.Manifest.PackId,
            context.PlatformVersion,
            context.RecordedAtUtc,
            input,
            result);
    }
}
