using NormaCase.Application.Retention;
using NormaCase.Domain.Cases;
using Xunit;

namespace NormaCase.Application.Tests;

public sealed class RetentionPlanningTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 11, 12, 0, 0, TimeSpan.Zero);
    private static RetentionArtifact Artifact(string id, RetentionStore store, DateTimeOffset? until,
        RetentionHold hold = RetentionHold.Released) => new(id, store, until, hold);

    [Fact]
    public void Expired_primary_copies_do_not_hide_unexpired_backup_or_legal_hold()
    {
        var plan = new RetentionPlanner().Plan(new CaseId("synthetic-retention-a"), "synthetic-policy", "1",
            Now, true, [Artifact("database", RetentionStore.CaseDatabase, Now.AddDays(-1)),
            Artifact("document", RetentionStore.Documents, Now.AddDays(-1)),
            Artifact("backup", RetentionStore.Backups, Now.AddDays(1)),
            Artifact("audit", RetentionStore.Audit, Now.AddDays(-1), RetentionHold.Active)]);
        Assert.False(plan.AllArtifactsEligibleForReview);
        Assert.Equal(RetentionDisposition.Retain, plan.Artifacts.Single(x => x.ArtifactId == "backup").Disposition);
        Assert.Equal(RetentionDisposition.RetainLegalHold, plan.Artifacts.Single(x => x.ArtifactId == "audit").Disposition);
        Assert.False(plan.DeletionAuthorized);
    }

    [Fact]
    public void Fully_expired_inventory_requires_review_and_never_authorizes_deletion()
    {
        var plan = new RetentionPlanner().Plan(new CaseId("synthetic-retention-b"), "synthetic-policy", "2",
            Now, true, Enum.GetValues<RetentionStore>().Select((store, i) => Artifact("copy-" + i, store, Now)));
        Assert.True(plan.AllArtifactsEligibleForReview);
        Assert.All(plan.Artifacts, item => Assert.Equal(RetentionDisposition.EligibleForReview, item.Disposition));
        Assert.False(plan.DeletionAuthorized);
        Assert.Equal("2", plan.PolicyVersion);
    }

    [Fact]
    public void Unknown_hold_expiry_or_inventory_never_becomes_eligible()
    {
        var planner = new RetentionPlanner();
        var input = new[] { Artifact("unknown-hold", RetentionStore.Documents, Now, RetentionHold.Unknown),
            Artifact("unknown-expiry", RetentionStore.OutboundExports, null) };
        var plan = planner.Plan(new CaseId("synthetic-retention-c"), "synthetic-policy", "1", Now, true, input);
        Assert.All(plan.Artifacts, item => Assert.Equal(RetentionDisposition.HumanReview, item.Disposition));
        var incomplete = planner.Plan(new CaseId("synthetic-retention-c"), "synthetic-policy", "1", Now, false,
            [Artifact("database", RetentionStore.CaseDatabase, Now)]);
        Assert.Equal(RetentionReason.InventoryIncomplete, Assert.Single(incomplete.Artifacts).Reason);
        Assert.False(incomplete.AllArtifactsEligibleForReview);
    }

    [Fact]
    public void Restored_copies_are_replanned_with_current_hold_and_inventory()
    {
        var planner = new RetentionPlanner();
        var caseId = new CaseId("synthetic-retention-restore");
        var before = planner.Plan(caseId, "synthetic-policy", "1", Now, true,
            [Artifact("database", RetentionStore.CaseDatabase, Now)]);
        Assert.True(before.AllArtifactsEligibleForReview);
        var restored = planner.Plan(caseId, "synthetic-policy", "2", Now.AddDays(1), true,
            [Artifact("database", RetentionStore.CaseDatabase, Now, RetentionHold.Active),
            Artifact("restored-backup", RetentionStore.Backups, null)]);
        Assert.False(restored.AllArtifactsEligibleForReview);
        Assert.False(restored.DeletionAuthorized);
        Assert.True(before.AllArtifactsEligibleForReview); // Original plan remains a snapshot.
    }

    [Fact]
    public void Caller_mutation_cannot_rewrite_plan_and_order_is_deterministic()
    {
        var input = new List<RetentionArtifact> { Artifact("z", RetentionStore.Documents, Now), Artifact("a", RetentionStore.CaseDatabase, Now) };
        var plan = new RetentionPlanner().Plan(new CaseId("synthetic-retention-d"), "synthetic-policy", "1", Now, true, input);
        input.Clear();
        Assert.Equal(new[] { "a", "z" }, plan.Artifacts.Select(item => item.ArtifactId));
        Assert.Throws<NotSupportedException>(() => ((IList<RetentionArtifactPlan>)plan.Artifacts).Clear());
    }

    [Fact]
    public void Duplicate_empty_unknown_enum_and_non_UTC_metadata_are_rejected()
    {
        var planner = new RetentionPlanner();
        var caseId = new CaseId("synthetic-retention-e");
        var one = Artifact("copy", RetentionStore.Documents, Now);
        Assert.Throws<ArgumentException>(() => planner.Plan(caseId, "p", "1", Now, true, [one, one]));
        Assert.Throws<ArgumentException>(() => planner.Plan(caseId, "", "1", Now, true, [one]));
        Assert.Throws<ArgumentException>(() => planner.Plan(caseId, "p", "1", Now, true, []));
        Assert.Throws<ArgumentException>(() => planner.Plan(caseId, "p", "1", Now.ToOffset(TimeSpan.FromHours(1)), true, [one]));
        Assert.Throws<ArgumentException>(() => planner.Plan(caseId, "p", "1", Now, true,
            [Artifact("copy", (RetentionStore)999, Now)]));
        Assert.Throws<ArgumentException>(() => planner.Plan(caseId, "p", "1", Now, true,
            [Artifact("copy", RetentionStore.Documents, Now, (RetentionHold)999)]));
        Assert.Throws<ArgumentException>(() => planner.Plan(caseId, "p", "1", Now, true,
            [Artifact("copy", RetentionStore.Documents, Now.ToOffset(TimeSpan.FromHours(1)))]));
    }

    [Fact]
    public void Empty_case_default_dates_and_excessive_inventory_fail_before_planning()
    {
        var planner = new RetentionPlanner();
        var one = Artifact("copy", RetentionStore.Documents, Now);
        Assert.Throws<ArgumentException>(() => planner.Plan(default, "p", "1", Now, true, [one]));
        Assert.Throws<ArgumentException>(() => planner.Plan(new CaseId("synthetic-limit"), "p", "1", default, true, [one]));
        Assert.Throws<ArgumentException>(() => planner.Plan(new CaseId("synthetic-limit"), "p", "1", Now, true,
            [Artifact("copy", RetentionStore.Documents, default(DateTimeOffset))]));
        Assert.Throws<ArgumentException>(() => planner.Plan(new CaseId("synthetic-limit"), "p", "1", Now, true,
            Enumerable.Range(0, RetentionPlanner.MaximumArtifacts + 1).Select(i => Artifact("copy-" + i, RetentionStore.Documents, Now))));
    }
}
