using NormaCase.Application.Knowledge;
using Xunit;

namespace NormaCase.Application.Tests;

public sealed class KnowledgeEvidenceTests
{
    [Fact]
    public void Evidence_preserves_exact_text_and_rejects_invalid_or_excessive_content()
    {
        var original = "Synthetische Quelle\r\n  äöü\t\n";
        var artifact = new KnowledgeEvidenceArtifact("evidence-1", "SOURCE", "Synthetischer Titel", original, "proposer", DateTimeOffset.UnixEpoch);
        Assert.Equal(original, artifact.Content);
        Assert.Equal(64, artifact.Sha256.Length);
        Assert.NotEqual(artifact.Sha256, new KnowledgeEvidenceArtifact("evidence-2", "SOURCE", "Titel", original.Trim(), "proposer", DateTimeOffset.UnixEpoch).Sha256);
        Assert.Throws<ArgumentException>(() => new KnowledgeEvidenceArtifact("id", "SCRIPT", "Titel", original, "actor", DateTimeOffset.UnixEpoch));
        Assert.Throws<ArgumentException>(() => new KnowledgeEvidenceArtifact("id", "TESTS", "Titel", "\0", "actor", DateTimeOffset.UnixEpoch));
        Assert.Throws<ArgumentException>(() => new KnowledgeEvidenceArtifact("id", "TESTS", "Titel", "\ud800", "actor", DateTimeOffset.UnixEpoch));
        Assert.Throws<ArgumentException>(() => new KnowledgeEvidenceArtifact("id", "TESTS", "Titel", new string('ä', 32769), "actor", DateTimeOffset.UnixEpoch));
    }
}
