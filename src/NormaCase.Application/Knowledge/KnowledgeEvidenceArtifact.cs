using System.Security.Cryptography;
using System.Text;

namespace NormaCase.Application.Knowledge;

public sealed record KnowledgeEvidenceArtifact
{
    public const int MaximumContentBytes = 65536;
    public string EvidenceId { get; }
    public string Kind { get; }
    public string Title { get; }
    public string Content { get; }
    public string Sha256 { get; }
    public string RecordedByActorId { get; }
    public DateTimeOffset RecordedAtUtc { get; }

    public KnowledgeEvidenceArtifact(string evidenceId, string kind, string title, string content,
        string recordedByActorId, DateTimeOffset recordedAtUtc)
    {
        EvidenceId = KnowledgeGovernanceValidation.Text(evidenceId);
        if (kind is not ("SOURCE" or "IMPACT" or "TESTS")) throw new ArgumentException("Invalid evidence kind.", nameof(kind));
        Kind = kind;
        Title = KnowledgeGovernanceValidation.Text(title);
        if (string.IsNullOrWhiteSpace(content) || content.Any(c => char.IsControl(c) && c is not ('\r' or '\n' or '\t')))
            throw new ArgumentException("Invalid evidence content.", nameof(content));
        byte[] bytes;
        try { bytes = new UTF8Encoding(false, true).GetBytes(content); }
        catch (EncoderFallbackException) { throw new ArgumentException("Invalid evidence encoding.", nameof(content)); }
        if (bytes.Length > MaximumContentBytes) throw new ArgumentException("Evidence content exceeds limit.", nameof(content));
        Content = content;
        Sha256 = Convert.ToHexStringLower(SHA256.HashData(bytes));
        RecordedByActorId = KnowledgeGovernanceValidation.Text(recordedByActorId);
        RecordedAtUtc = KnowledgeGovernanceValidation.Utc(recordedAtUtc);
    }
}

public interface IKnowledgeEvidenceStore
{
    Task<KnowledgeEvidenceArtifact> RegisterAsync(KnowledgeEvidenceArtifact artifact, CancellationToken token = default);
    Task<KnowledgeEvidenceArtifact?> LoadAsync(string evidenceId, CancellationToken token = default);
}

public sealed class KnowledgeEvidenceIntegrityException : Exception
{
    public KnowledgeEvidenceIntegrityException() : base("Stored Knowledge evidence failed verification.") { }
}
