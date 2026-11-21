using NormaCase.Application.Knowledge;
using Xunit;

namespace NormaCase.Application.Tests;

public sealed class KnowledgeActivationSelectionTests
{
    [Fact]
    public void Selection_requires_an_exact_bounded_identity_revision_and_hash()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new KnowledgeActivationSelection("pack", 0, "release", new string('a', 64)));
        Assert.Throws<ArgumentException>(() => new KnowledgeActivationSelection(" pack", 1, "release", new string('a', 64)));
        Assert.Throws<ArgumentException>(() => new KnowledgeActivationSelection("pack", 1, "release", new string('A', 64)));
        Assert.Throws<ArgumentException>(() => new KnowledgeActivationSelection("pack", 1, "release", new string('a', 63)));
        var selection = new KnowledgeActivationSelection("pack", 1, "release", new string('a', 64));
        Assert.Equal(1, selection.ActivationRevision);
    }
}
