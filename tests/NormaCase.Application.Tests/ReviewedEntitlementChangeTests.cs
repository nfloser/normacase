using NormaCase.Application.Authorization;
using Xunit;

namespace NormaCase.Application.Tests;

public sealed class ReviewedEntitlementChangeTests
{
    [Fact]
    public async Task Service_requires_a_distinct_decision_actor_when_policy_demands_it()
    {
        var store = new RecordingStore();
        var service = new ReviewedEntitlementChangeService(
            store, new(RequireDistinctDecisionActor: true));
        var proposal = Proposal("synthetic-local:access-proposer");
        await service.ProposeAsync(proposal);

        await Assert.ThrowsAsync<EntitlementSeparationOfDutiesException>(() =>
            service.DecideAsync(new(proposal.ChangeId, proposal.ProposerActorId,
                new DateTimeOffset(2026, 10, 4, 12, 1, 0, TimeSpan.Zero), true,
                "Synthetische Selbstfreigabe")));
        Assert.Null(store.Decision);

        var decision = new EntitlementChangeDecision(
            proposal.ChangeId, "synthetic-local:access-approver",
            new DateTimeOffset(2026, 10, 4, 12, 2, 0, TimeSpan.Zero), true,
            "Synthetische Gegenprüfung");
        await service.DecideAsync(decision);
        Assert.Same(decision, store.Decision);
        Assert.True(store.RequireDistinctDecisionActor);
    }

    [Fact]
    public void Proposal_detaches_and_validates_complete_entitlement_sets()
    {
        var actions = new[] { "READ", "ACCEPT" };
        var cases = new[] { "synthetic-case-a", "synthetic-case-b" };
        var proposal = new EntitlementChangeProposal(
            "entitlement-change-a", "synthetic-local:user-alice", 7,
            actions, cases, "synthetic-local:access-proposer",
            new DateTimeOffset(2026, 10, 4, 12, 0, 0, TimeSpan.Zero),
            "Synthetische Rechteänderung");
        actions[0] = "OVERRIDE";
        cases[0] = "substituted-case";

        Assert.Equal(new[] { "ACCEPT", "READ" }, proposal.Actions);
        Assert.Equal(new[] { "synthetic-case-a", "synthetic-case-b" }, proposal.CaseIds);
        Assert.Throws<ArgumentException>(() => new EntitlementChangeProposal(
            "entitlement-change-b", proposal.TargetActorId, 0,
            ["READ", "READ"], ["synthetic-case-a"], proposal.ProposerActorId,
            proposal.ProposedAtUtc, proposal.Reason));
        Assert.Throws<ArgumentException>(() => new EntitlementChangeProposal(
            "entitlement-change-c", proposal.TargetActorId, 0,
            ["read"], ["synthetic-case-a"], proposal.ProposerActorId,
            proposal.ProposedAtUtc, proposal.Reason));
    }

    private static EntitlementChangeProposal Proposal(string proposer) => new(
        "entitlement-change-1", "synthetic-local:user-alice", 0,
        ["READ", "ACCEPT"], ["demo-g-supported"], proposer,
        new DateTimeOffset(2026, 10, 4, 12, 0, 0, TimeSpan.Zero),
        "Synthetischer Antrag");

    private sealed class RecordingStore : IReviewedEntitlementChangeStore
    {
        private EntitlementChangeProposal? proposal;
        internal EntitlementChangeDecision? Decision { get; private set; }
        internal bool RequireDistinctDecisionActor { get; private set; }

        public Task<EntitlementChangeRecord> ProposeAsync(
            EntitlementChangeProposal proposal, CancellationToken cancellationToken = default)
        {
            this.proposal = proposal;
            return Task.FromResult(new EntitlementChangeRecord(proposal, null));
        }

        public Task<EntitlementChangeRecord> DecideAsync(
            EntitlementChangeDecision decision, bool requireDistinctDecisionActor,
            CancellationToken cancellationToken = default)
        {
            Decision = decision;
            RequireDistinctDecisionActor = requireDistinctDecisionActor;
            return Task.FromResult(new EntitlementChangeRecord(proposal!, decision));
        }

        public Task<EntitlementChangeRecord?> LoadChangeAsync(
            string changeId, CancellationToken cancellationToken = default)
            => Task.FromResult(proposal is null ? null : new EntitlementChangeRecord(proposal, Decision));

        public Task<IdentityEntitlementState> LoadEffectiveAsync(
            string actorId, CancellationToken cancellationToken = default)
            => Task.FromResult(new IdentityEntitlementState(actorId, 0, [], []));
    }
}
