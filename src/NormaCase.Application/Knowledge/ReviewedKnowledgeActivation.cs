using System.Text.RegularExpressions;

namespace NormaCase.Application.Knowledge;

// These are technical governance records, never a promotion of domain validation.
public sealed record KnowledgeChangeProposal
{
    public string ChangeId { get; }
    public string PackId { get; }
    public string ReleaseId { get; }
    public string Sha256 { get; }
    public string SourceReference { get; }
    public string ImpactReference { get; }
    public string TestReference { get; }
    public string ProposerActorId { get; }
    public DateTimeOffset ProposedAtUtc { get; }

    public KnowledgeChangeProposal(string changeId, string packId, string releaseId, string sha256,
        string sourceReference, string impactReference, string testReference,
        string proposerActorId, DateTimeOffset proposedAtUtc)
    {
        ChangeId = KnowledgeGovernanceValidation.Text(changeId);
        PackId = KnowledgeGovernanceValidation.Text(packId);
        ReleaseId = KnowledgeGovernanceValidation.Text(releaseId);
        if (sha256 is null || !Regex.IsMatch(sha256, "\\A[0-9a-f]{64}\\z", RegexOptions.CultureInvariant))
            throw new ArgumentException("Invalid release hash.", nameof(sha256));
        Sha256 = sha256;
        SourceReference = KnowledgeGovernanceValidation.Text(sourceReference);
        ImpactReference = KnowledgeGovernanceValidation.Text(impactReference);
        TestReference = KnowledgeGovernanceValidation.Text(testReference);
        ProposerActorId = KnowledgeGovernanceValidation.Text(proposerActorId);
        ProposedAtUtc = KnowledgeGovernanceValidation.Utc(proposedAtUtc);
    }
}

public sealed record KnowledgeChangeDecision
{
    public string ChangeId { get; }
    public string ReviewerActorId { get; }
    public DateTimeOffset ReviewedAtUtc { get; }
    public bool Approved { get; }
    public string Reason { get; }
    public KnowledgeChangeDecision(string changeId, string reviewerActorId,
        DateTimeOffset reviewedAtUtc, bool approved, string reason)
    {
        ChangeId = KnowledgeGovernanceValidation.Text(changeId);
        ReviewerActorId = KnowledgeGovernanceValidation.Text(reviewerActorId);
        ReviewedAtUtc = KnowledgeGovernanceValidation.Utc(reviewedAtUtc);
        Approved = approved;
        Reason = KnowledgeGovernanceValidation.Text(reason, 1000);
    }
}

public sealed record KnowledgeActivationCommand
{
    public string ChangeId { get; }
    public long ExpectedRevision { get; }
    public string ActorId { get; }
    public DateTimeOffset ActivatedAtUtc { get; }
    public KnowledgeActivationCommand(string changeId, long expectedRevision, string actorId, DateTimeOffset activatedAtUtc)
    {
        ChangeId = KnowledgeGovernanceValidation.Text(changeId);
        if (expectedRevision < 0 || expectedRevision == long.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(expectedRevision));
        ExpectedRevision = expectedRevision;
        ActorId = KnowledgeGovernanceValidation.Text(actorId);
        ActivatedAtUtc = KnowledgeGovernanceValidation.Utc(activatedAtUtc);
    }
}

public sealed record KnowledgeChangeRecord(KnowledgeChangeProposal Proposal, KnowledgeChangeDecision? Decision);
public sealed record KnowledgeActivationRecord(string PackId, long Revision, string ChangeId,
    string ReleaseId, string Sha256, string ActorId, DateTimeOffset ActivatedAtUtc);

public interface IReviewedKnowledgeActivationStore
{
    Task<KnowledgeChangeRecord> ProposeAsync(KnowledgeChangeProposal proposal, CancellationToken token = default);
    Task<KnowledgeChangeRecord> DecideAsync(KnowledgeChangeDecision decision, CancellationToken token = default);
    Task<KnowledgeActivationRecord> ActivateAsync(KnowledgeActivationCommand command, CancellationToken token = default);
    Task<KnowledgeChangeRecord?> LoadChangeAsync(string changeId, CancellationToken token = default);
    Task<KnowledgeActivationRecord?> LoadActiveAsync(string packId, CancellationToken token = default);
    // Historical access is bounded and exact; there is no implicit "latest release" lookup.
    Task<KnowledgeActivationRecord?> LoadActivationAsync(string packId, long revision, CancellationToken token = default);
}

public static class KnowledgeGovernanceValidation
{
    public static string Text(string value, int maximum = 256)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > maximum || value.Any(char.IsControl)
            || value != value.Trim()) throw new ArgumentException("Invalid governance value.");
        return value;
    }
    public static DateTimeOffset Utc(DateTimeOffset value)
    {
        // PostgreSQL stores microseconds. Reject values that would lose exact audit time.
        if (value.Offset != TimeSpan.Zero || value.Ticks % 10 != 0)
            throw new ArgumentException("Exact UTC microsecond timestamp required.");
        return value;
    }
}

public sealed class KnowledgeGovernanceConflictException : Exception
{
    public KnowledgeGovernanceConflictException() : base("Knowledge governance state conflicts with the command.") { }
}
public sealed class KnowledgeGovernanceNotFoundException : Exception
{
    public KnowledgeGovernanceNotFoundException() : base("Knowledge governance record was not found.") { }
}
public sealed class KnowledgeGovernanceStorageException : Exception
{
    public KnowledgeGovernanceStorageException() : base("Knowledge governance storage operation failed.") { }
}
