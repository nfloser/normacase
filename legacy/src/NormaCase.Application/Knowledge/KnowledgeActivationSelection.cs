using System.Text.RegularExpressions;
using NormaCase.Knowledge.Catalog;

namespace NormaCase.Application.Knowledge;

public sealed record KnowledgeActivationSelection
{
    public string PackId { get; }
    public long ActivationRevision { get; }
    public string ReleaseId { get; }
    public string Sha256 { get; }
    public KnowledgeActivationSelection(string packId, long activationRevision, string releaseId, string sha256)
    {
        PackId = KnowledgeGovernanceValidation.Text(packId);
        ReleaseId = KnowledgeGovernanceValidation.Text(releaseId);
        if (activationRevision < 1) throw new ArgumentOutOfRangeException(nameof(activationRevision));
        ActivationRevision = activationRevision;
        if (sha256 is null || !Regex.IsMatch(sha256, "\\A[0-9a-f]{64}\\z", RegexOptions.CultureInvariant))
            throw new ArgumentException("Exact release hash required.", nameof(sha256));
        Sha256 = sha256;
    }
}

// Selection is explicit and historical. There is no lookup of the current/latest activation.
public sealed class KnowledgeActivationSelectionService(
    IReviewedKnowledgeActivationStore governance, IKnowledgeReleaseStore releases, IKnowledgeEvidenceStore evidence)
{
    public async Task<KnowledgeReleaseArtifact> LoadAsync(KnowledgeActivationSelection selection, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(selection);
        var activation = await governance.LoadActivationAsync(selection.PackId, selection.ActivationRevision, token);
        if (activation is null || activation.PackId != selection.PackId || activation.Revision != selection.ActivationRevision
            || activation.ReleaseId != selection.ReleaseId || activation.Sha256 != selection.Sha256)
            throw new KnowledgeActivationSelectionException();
        var change = await governance.LoadChangeAsync(activation.ChangeId, token);
        if (change is null || change.Decision is not { Approved: true } decision
            || change.Proposal.ChangeId != activation.ChangeId || change.Proposal.PackId != selection.PackId
            || change.Proposal.ReleaseId != selection.ReleaseId || change.Proposal.Sha256 != selection.Sha256
            || decision.ChangeId != activation.ChangeId || change.Proposal.ProposerActorId == decision.ReviewerActorId
            || decision.ReviewedAtUtc < change.Proposal.ProposedAtUtc || activation.ActivatedAtUtc < decision.ReviewedAtUtc)
            throw new KnowledgeActivationSelectionException();
        foreach (var (id, kind) in new[] { (change.Proposal.SourceReference, "SOURCE"),
            (change.Proposal.ImpactReference, "IMPACT"), (change.Proposal.TestReference, "TESTS") })
        {
            var artifact = await evidence.LoadAsync(id, token);
            if (artifact is null || artifact.EvidenceId != id || artifact.Kind != kind
                || artifact.RecordedAtUtc > change.Proposal.ProposedAtUtc)
                throw new KnowledgeActivationSelectionException();
        }
        var release = await releases.LoadAsync(selection.PackId, selection.ReleaseId, token);
        if (release is null || release.PackId != selection.PackId || release.ReleaseId != selection.ReleaseId
            || release.Sha256 != selection.Sha256)
            throw new KnowledgeActivationSelectionException();
        return release;
    }
}

public sealed class KnowledgeActivationSelectionException : Exception
{
    public KnowledgeActivationSelectionException() : base("Exact reviewed Knowledge selection failed verification.") { }
}
