using System.Collections.ObjectModel;
using NormaCase.Application.Assessments;
using NormaCase.Domain.Cases;
using NormaCase.Domain.Evidence;
using NormaCase.Knowledge.Model;
using NormaCase.Knowledge.Validation;

namespace NormaCase.Application.Intake;

public sealed class NormalizedIntakeRecord
{
    internal NormalizedIntakeRecord(NormalizedIntakeRequest request, KnowledgePack pack,
        AssessmentInputSnapshot input, Dictionary<string,IReadOnlyList<string>> references)
    {
        CaseId = request.CaseId; CaseTypeId = request.CaseTypeId; Provenance = request.Provenance;
        KnowledgePackId = pack.Manifest.PackId; KnowledgeRelease = pack.Manifest.ReleaseId;
        Input = input;
        EvidenceReferences = new ReadOnlyDictionary<string,IReadOnlyList<string>>(references);
    }
    public CaseId CaseId { get; }
    public string CaseTypeId { get; }
    public IntakeProvenance Provenance { get; }
    public string KnowledgePackId { get; }
    public string KnowledgeRelease { get; }
    public AssessmentInputSnapshot Input { get; }
    public IReadOnlyDictionary<string,IReadOnlyList<string>> EvidenceReferences { get; }

    /// <summary>Semantic retry equality; message id and local receipt time are delivery metadata.</summary>
    public bool HasSameContent(NormalizedIntakeRecord other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return CaseId == other.CaseId && CaseTypeId == other.CaseTypeId
            && KnowledgePackId == other.KnowledgePackId && KnowledgeRelease == other.KnowledgeRelease
            && Provenance.SourceSystemId == other.Provenance.SourceSystemId
            && Provenance.UpstreamCaseId == other.Provenance.UpstreamCaseId
            && Provenance.UpstreamRevision == other.Provenance.UpstreamRevision
            && Provenance.AdapterId == other.Provenance.AdapterId && Provenance.AdapterVersion == other.Provenance.AdapterVersion
            && Input.AssessmentDate == other.Input.AssessmentDate
            && Equal(Input.Facts, other.Input.Facts) && Equal(Input.Evidence, other.Input.Evidence)
            && EvidenceReferences.Count == other.EvidenceReferences.Count
            && EvidenceReferences.All(item => other.EvidenceReferences.TryGetValue(item.Key, out var references)
                && item.Value.SequenceEqual(references, StringComparer.Ordinal));
    }
    private static bool Equal<T>(IReadOnlyDictionary<string,T> left, IReadOnlyDictionary<string,T> right)
        => left.Count == right.Count && left.All(item => right.TryGetValue(item.Key, out var value)
            && EqualityComparer<T>.Default.Equals(item.Value, value));
}

public enum IntakeAcceptance { Accepted, Duplicate }
public sealed record IntakeReceipt(IntakeAcceptance Acceptance, NormalizedIntakeRecord Record);

/// <summary>
/// Atomic uniqueness: (source, message) AND (source, upstream case, revision).
/// Same semantic content returns the ORIGINAL receipt; any conflicting binding throws.
/// Implementations must preserve case mapping across revisions and reject stale revisions.
/// Each platform CaseId is owned by exactly one (source system, upstream case) stream.
/// </summary>
public interface INormalizedIntakeStore
{
    Task<IntakeReceipt> AppendAsync(NormalizedIntakeRecord record, CancellationToken cancellationToken = default);
}
public sealed class IntakeConflictException : InvalidOperationException
{
    public IntakeConflictException() : base("Normalized intake identity or content conflicts with a recorded delivery.") { }
}

public sealed class NormalizedIntakeService
{
    private readonly INormalizedIntakeStore store;
    public NormalizedIntakeService(INormalizedIntakeStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        this.store = store;
    }
    public async Task<IntakeReceipt> AcceptAsync(NormalizedIntakeRequest request, KnowledgePack pack,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var record = Normalize(request, pack);
        var receipt = await store.AppendAsync(record, cancellationToken);
        if (receipt is null || receipt.Record is null || !Enum.IsDefined(receipt.Acceptance)
            || !receipt.Record.HasSameContent(record)
            || receipt.Acceptance == IntakeAcceptance.Accepted
                && (receipt.Record.Provenance.MessageId != record.Provenance.MessageId
                    || receipt.Record.Provenance.ReceivedAtUtc != record.Provenance.ReceivedAtUtc))
            throw new IntakeConflictException();
        return receipt;
    }
    public NormalizedIntakeRecord Normalize(NormalizedIntakeRequest request, KnowledgePack pack)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(pack);
        new KnowledgePackValidator().ValidateOrThrow(pack);
        IntakeBoundary.Count(pack.Fields.Count);
        IntakeBoundary.Count(pack.EvidenceRequirements.Count);
        foreach (var field in pack.Fields) IntakeBoundary.Identifier(field.Id);
        foreach (var requirement in pack.EvidenceRequirements) IntakeBoundary.Identifier(requirement.Id);
        var fields = pack.Fields.ToDictionary(field => field.Id, StringComparer.Ordinal);
        var requirements = pack.EvidenceRequirements.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var item in request.Facts)
        {
            if (!fields.TryGetValue(item.Key, out var field) || !item.Value.IsUnknown
                && (field.Type == "truth" && item.Value.Kind != CaseValueKind.Truth
                    || field.Type == "number" && item.Value.Kind != CaseValueKind.Number))
                throw new ArgumentException("Normalized intake facts do not match the selected schema.");
        }
        if (request.Evidence.Keys.Concat(request.EvidenceReferences.Keys).Any(key => !requirements.Contains(key)))
            throw new ArgumentException("Normalized intake evidence does not match the selected schema.");
        var facts = fields.Keys.ToDictionary(key => key, key => request.Facts.GetValueOrDefault(key, CaseValue.Unknown), StringComparer.Ordinal);
        var evidence = requirements.ToDictionary(key => key, key => request.Evidence.GetValueOrDefault(key, EvidenceStatus.Missing), StringComparer.Ordinal);
        var references = requirements.ToDictionary(key => key,
            key => request.EvidenceReferences.GetValueOrDefault(key, Array.AsReadOnly(Array.Empty<string>())), StringComparer.Ordinal);
        foreach (var key in requirements)
            if (evidence[key] == EvidenceStatus.Present && references[key].Count == 0
                || evidence[key] == EvidenceStatus.Missing && references[key].Count > 0)
                throw new ArgumentException("Evidence availability and normalized references must agree.");
        return new(request, pack, new(request.AssessmentDate, facts, evidence), references);
    }
}
