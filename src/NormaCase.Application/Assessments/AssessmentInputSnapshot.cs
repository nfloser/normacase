using System.Collections.ObjectModel;
using NormaCase.Domain.Cases;
using NormaCase.Domain.Evidence;

namespace NormaCase.Application.Assessments;

public sealed record AssessmentInputSnapshot
{
    public AssessmentInputSnapshot(
        DateOnly assessmentDate,
        IReadOnlyDictionary<string, CaseValue> facts,
        IReadOnlyDictionary<string, EvidenceStatus> evidence)
    {
        ArgumentNullException.ThrowIfNull(facts);
        ArgumentNullException.ThrowIfNull(evidence);

        AssessmentDate = assessmentDate;
        Facts = ReadOnly(facts);
        Evidence = ReadOnly(evidence);
    }

    public DateOnly AssessmentDate { get; }
    public IReadOnlyDictionary<string, CaseValue> Facts { get; }
    public IReadOnlyDictionary<string, EvidenceStatus> Evidence { get; }

    private static IReadOnlyDictionary<string, TValue> ReadOnly<TValue>(
        IReadOnlyDictionary<string, TValue> source)
        => new ReadOnlyDictionary<string, TValue>(
            source.ToDictionary(
                item => item.Key,
                item => item.Value,
                StringComparer.Ordinal));
}
