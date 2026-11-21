using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using NormaCase.Domain.Cases;
using NormaCase.Domain.Evidence;

namespace NormaCase.Application.Intake;

internal static class IntakeBoundary
{
    internal static void Identifier(string value)
    {
        if (value is null || value.Length > 128 || !Regex.IsMatch(value, "\\A[A-Za-z0-9][A-Za-z0-9._@-]*\\z", RegexOptions.CultureInvariant))
            throw new ArgumentException("Invalid normalized intake identifier.");
    }
    internal static void Count(int count)
    {
        if (count > 256) throw new ArgumentException("Normalized intake collection exceeds its limit.");
    }
}

public sealed class IntakeProvenance
{
    public IntakeProvenance(string sourceSystemId, string upstreamCaseId, string messageId,
        long upstreamRevision, string adapterId, int adapterVersion, DateTimeOffset receivedAtUtc)
    {
        IntakeBoundary.Identifier(sourceSystemId);
        IntakeBoundary.Identifier(upstreamCaseId);
        IntakeBoundary.Identifier(messageId);
        IntakeBoundary.Identifier(adapterId);
        if (upstreamRevision < 1) throw new ArgumentOutOfRangeException(nameof(upstreamRevision));
        if (adapterVersion < 1) throw new ArgumentOutOfRangeException(nameof(adapterVersion));
        if (receivedAtUtc == default || receivedAtUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException("Explicit intake UTC timestamp required.", nameof(receivedAtUtc));
        SourceSystemId = sourceSystemId; UpstreamCaseId = upstreamCaseId; MessageId = messageId;
        UpstreamRevision = upstreamRevision; AdapterId = adapterId; AdapterVersion = adapterVersion;
        ReceivedAtUtc = receivedAtUtc;
    }
    public string SourceSystemId { get; }
    public string UpstreamCaseId { get; }
    public string MessageId { get; }
    public long UpstreamRevision { get; }
    public string AdapterId { get; }
    public int AdapterVersion { get; }
    public DateTimeOffset ReceivedAtUtc { get; }
}

public sealed class NormalizedIntakeRequest
{
    public NormalizedIntakeRequest(CaseId caseId, string caseTypeId, IntakeProvenance provenance,
        DateOnly assessmentDate, IReadOnlyDictionary<string, CaseValue> facts,
        IReadOnlyDictionary<string, EvidenceStatus> evidence,
        IReadOnlyDictionary<string, IReadOnlyList<string>> evidenceReferences)
    {
        if (caseId.IsEmpty) throw new ArgumentException("Explicit case identity required.", nameof(caseId));
        IntakeBoundary.Identifier(caseId.Value);
        IntakeBoundary.Identifier(caseTypeId);
        ArgumentNullException.ThrowIfNull(provenance);
        ArgumentNullException.ThrowIfNull(facts);
        ArgumentNullException.ThrowIfNull(evidence);
        ArgumentNullException.ThrowIfNull(evidenceReferences);
        if (assessmentDate == default) throw new ArgumentException("Explicit assessment date required.", nameof(assessmentDate));
        IntakeBoundary.Count(facts.Count); IntakeBoundary.Count(evidence.Count); IntakeBoundary.Count(evidenceReferences.Count);
        foreach (var key in facts.Keys.Concat(evidence.Keys).Concat(evidenceReferences.Keys)) IntakeBoundary.Identifier(key);
        if (facts.Values.Any(value => !Enum.IsDefined(value.Kind)
            || value.Truth is not null && !Enum.IsDefined(value.Truth.Value)))
            throw new ArgumentException("Invalid normalized fact value.", nameof(facts));
        if (evidence.Values.Any(value => !Enum.IsDefined(value)))
            throw new ArgumentException("Invalid normalized evidence status.", nameof(evidence));
        var references = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        foreach (var item in evidenceReferences)
        {
            if (item.Value is null || item.Value.Count > 32) throw new ArgumentException("Invalid normalized evidence references.");
            foreach (var reference in item.Value) IntakeBoundary.Identifier(reference);
            if (item.Value.Distinct(StringComparer.Ordinal).Count() != item.Value.Count)
                throw new ArgumentException("Duplicate normalized evidence reference.");
            references.Add(item.Key, Array.AsReadOnly(item.Value.Order(StringComparer.Ordinal).ToArray()));
        }
        CaseId = caseId; CaseTypeId = caseTypeId; Provenance = provenance; AssessmentDate = assessmentDate;
        Facts = new ReadOnlyDictionary<string, CaseValue>(facts.ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal));
        Evidence = new ReadOnlyDictionary<string, EvidenceStatus>(evidence.ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal));
        EvidenceReferences = new ReadOnlyDictionary<string, IReadOnlyList<string>>(references);
    }
    public CaseId CaseId { get; }
    public string CaseTypeId { get; }
    public IntakeProvenance Provenance { get; }
    public DateOnly AssessmentDate { get; }
    public IReadOnlyDictionary<string, CaseValue> Facts { get; }
    public IReadOnlyDictionary<string, EvidenceStatus> Evidence { get; }
    public IReadOnlyDictionary<string, IReadOnlyList<string>> EvidenceReferences { get; }
}
