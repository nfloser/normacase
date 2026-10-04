using NormaCase.Application.Knowledge;
using Xunit;

namespace NormaCase.Application.Tests;

public sealed class ReviewedKnowledgeActivationTests
{
    [Fact]
    public void Governance_rejects_unbounded_values_invalid_hash_and_lossy_time()
    {
        var time = DateTimeOffset.UnixEpoch;
        Assert.Throws<ArgumentException>(() => Proposal(" ", new string('a', 64), time));
        Assert.Throws<ArgumentException>(() => Proposal(new string('c', 257), new string('a', 64), time));
        Assert.Throws<ArgumentException>(() => Proposal("change", new string('A', 64), time));
        Assert.Throws<ArgumentException>(() => Proposal("change", new string('a', 64), time.AddTicks(1)));
        Assert.Throws<ArgumentException>(() => Proposal("change", new string('a', 64), time.ToOffset(TimeSpan.FromHours(1))));
        Assert.Throws<ArgumentException>(() => new KnowledgeChangeDecision("change", "reviewer", time, true, "reason\ninput"));
        Assert.Throws<ArgumentOutOfRangeException>(() => new KnowledgeActivationCommand("change", -1, "activator", time));
        Assert.Throws<ArgumentOutOfRangeException>(() => new KnowledgeActivationCommand("change", long.MaxValue, "activator", time));
    }
    private static KnowledgeChangeProposal Proposal(string id, string hash, DateTimeOffset time)
        => new(id, "pack", "release", hash, "source:1", "impact:1", "tests:1", "proposer", time);
}
