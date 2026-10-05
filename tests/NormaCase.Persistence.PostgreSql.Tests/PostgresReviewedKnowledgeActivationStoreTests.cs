using System.Text.Json.Nodes;
using Npgsql;
using NormaCase.Application.Knowledge;
using NormaCase.Knowledge.Catalog;
using Xunit;

namespace NormaCase.Persistence.PostgreSql.Tests;

public sealed class PostgresReviewedKnowledgeActivationStoreTests
{
    private static readonly DateTimeOffset Time = DateTimeOffset.UnixEpoch;

    [Theory]
    [InlineData("a")]
    [InlineData("c")]
    public async Task Exact_selection_retains_review_and_evidence_and_never_follows_latest(string letter)
    {
        await using var source = Source();
        await new PostgresMigrationRunner(source).MigrateAsync();
        var releases = new PostgresKnowledgeReleaseStore(source);
        var evidence = new PostgresKnowledgeEvidenceStore(source);
        var governance = new PostgresReviewedKnowledgeActivationStore(source, requireRetainedEvidence: true);
        var artifact = await releases.RegisterAsync(Fixture(letter));
        var ids = new List<string>();
        foreach (var kind in new[] { "SOURCE", "IMPACT", "TESTS" })
        {
            var item = await evidence.RegisterAsync(new(Id(), kind, "Synthetischer Beleg", "Exakter synthetischer Inhalt", "proposer", Time));
            ids.Add(item.EvidenceId);
        }
        async Task<KnowledgeActivationRecord> Activate(KnowledgeReleaseArtifact release, long revision)
        {
            var proposal = new KnowledgeChangeProposal(Id(), release.PackId, release.ReleaseId, release.Sha256,
                ids[0], ids[1], ids[2], "proposer", Time);
            await governance.ProposeAsync(proposal);
            await governance.DecideAsync(new(proposal.ChangeId, "reviewer", Time, true, "Synthetisch geprüft"));
            return await governance.ActivateAsync(new(proposal.ChangeId, revision, "activator", Time));
        }
        var active = await Activate(artifact, 0);
        var selection = new KnowledgeActivationSelection(artifact.PackId, active.Revision, artifact.ReleaseId, artifact.Sha256);
        var service = new KnowledgeActivationSelectionService(governance, releases, evidence);
        Assert.Equal(artifact.KnowledgePackJson, (await service.LoadAsync(selection)).KnowledgePackJson);
        var node = JsonNode.Parse(artifact.KnowledgePackJson)!;
        node["manifest"]!["releaseId"] = Id();
        var newer = await releases.RegisterAsync(node.ToJsonString());
        await Activate(newer, 1);
        Assert.Equal(artifact.KnowledgePackJson, (await service.LoadAsync(selection)).KnowledgePackJson);
        await Assert.ThrowsAsync<KnowledgeActivationSelectionException>(() => service.LoadAsync(
            new(artifact.PackId, 99, artifact.ReleaseId, artifact.Sha256)));
        await Assert.ThrowsAsync<KnowledgeActivationSelectionException>(() => service.LoadAsync(
            new(artifact.PackId, 1, newer.ReleaseId, newer.Sha256)));
        await Assert.ThrowsAsync<KnowledgeActivationSelectionException>(() => service.LoadAsync(
            new(artifact.PackId, 1, artifact.ReleaseId, new string('0', 64))));
        await using var restartedSource = Source();
        Assert.Equal(artifact.KnowledgePackJson, (await new KnowledgeActivationSelectionService(
            new PostgresReviewedKnowledgeActivationStore(restartedSource), new PostgresKnowledgeReleaseStore(restartedSource),
            new PostgresKnowledgeEvidenceStore(restartedSource)).LoadAsync(selection)).KnowledgePackJson);
        var legacy = await releases.RegisterAsync(Fixture(letter));
        var legacyGovernance = new PostgresReviewedKnowledgeActivationStore(source);
        var legacyProposal = Proposal(legacy);
        await legacyGovernance.ProposeAsync(legacyProposal);
        await legacyGovernance.DecideAsync(new(legacyProposal.ChangeId, "reviewer", Time, true, "Historical reference only"));
        await legacyGovernance.ActivateAsync(new(legacyProposal.ChangeId, 0, "activator", Time));
        await Assert.ThrowsAsync<KnowledgeActivationSelectionException>(() => service.LoadAsync(
            new(legacy.PackId, 1, legacy.ReleaseId, legacy.Sha256)));
    }

    [Theory]
    [InlineData("a")]
    [InlineData("c")]
    public async Task Reviewed_activation_retains_exact_history_and_restart_without_validation_promotion(string letter)
    {
        await using var source = Source();
        await new PostgresMigrationRunner(source).MigrateAsync();
        var releases = new PostgresKnowledgeReleaseStore(source);
        var artifact = await releases.RegisterAsync(Fixture(letter));
        var store = new PostgresReviewedKnowledgeActivationStore(source);
        var first = Proposal(artifact);
        Assert.Null(await store.LoadActiveAsync(artifact.PackId));
        await store.ProposeAsync(first);
        await Assert.ThrowsAsync<KnowledgeGovernanceConflictException>(() => store.ActivateAsync(new(first.ChangeId, 0, "activator", Time)));
        await Assert.ThrowsAsync<KnowledgeGovernanceConflictException>(() => store.DecideAsync(new(first.ChangeId, "proposer", Time, true, "self review")));
        await Assert.ThrowsAsync<KnowledgeGovernanceConflictException>(() => store.DecideAsync(new(first.ChangeId, "reviewer", Time.AddSeconds(-1), true, "early")));
        Assert.Null((await store.LoadChangeAsync(first.ChangeId))!.Decision);
        await store.DecideAsync(new(first.ChangeId, "reviewer", Time.AddSeconds(1), true, "synthetic tests reviewed"));
        await Assert.ThrowsAsync<KnowledgeGovernanceConflictException>(() => store.ActivateAsync(new(first.ChangeId, 0, "activator", Time)));
        var active = await store.ActivateAsync(new(first.ChangeId, 0, "activator", Time.AddSeconds(2)));
        Assert.Equal(1, active.Revision);
        Assert.Equal(artifact.Sha256, active.Sha256);

        var node = JsonNode.Parse(artifact.KnowledgePackJson)!;
        node["manifest"]!["releaseId"] = Id();
        var next = await releases.RegisterAsync(node.ToJsonString());
        var second = Proposal(next);
        await store.ProposeAsync(second);
        await store.DecideAsync(new(second.ChangeId, "reviewer", Time.AddSeconds(1), true, "second exact release reviewed"));
        var newer = await store.ActivateAsync(new(second.ChangeId, 1, "activator", Time.AddSeconds(3)));
        Assert.Equal(2, newer.Revision);
        await Assert.ThrowsAsync<KnowledgeGovernanceConflictException>(() => store.ActivateAsync(new(first.ChangeId, 1, "activator", Time.AddSeconds(4))));
        // Explicit rollback is a new activation, never deletion of the intervening release.
        var rollback = await store.ActivateAsync(new(first.ChangeId, 2, "activator", Time.AddSeconds(4)));
        await using var restartedSource = Source();
        var restarted = new PostgresReviewedKnowledgeActivationStore(restartedSource);
        Assert.Equal(rollback, await restarted.LoadActiveAsync(artifact.PackId));
        Assert.Equal(active, await restarted.LoadActivationAsync(artifact.PackId, 1));
        Assert.Equal(newer, await restarted.LoadActivationAsync(artifact.PackId, 2));
        Assert.Equal(first, (await restarted.LoadChangeAsync(first.ChangeId))!.Proposal);
        Assert.Equal("SYNTHETIC", (await releases.LoadAsync(artifact.PackId, artifact.ReleaseId))!.ValidationLevel);
        Assert.Null(await store.LoadActivationAsync(artifact.PackId, 99));
        foreach (var table in new[] { "knowledge_changes", "knowledge_change_decisions", "knowledge_activations" })
        {
            foreach (var operation in new[] { $"UPDATE normacase.{table} SET change_id=change_id WHERE change_id=$1", $"DELETE FROM normacase.{table} WHERE change_id=$1" })
            {
                await using var mutation = source.CreateCommand(operation);
                mutation.Parameters.AddWithValue(first.ChangeId);
                await Assert.ThrowsAsync<PostgresException>(() => mutation.ExecuteNonQueryAsync());
            }
        }
    }

    [Fact]
    public async Task Rejection_hash_mismatch_duplicate_decision_and_competing_activation_fail_without_partial_history()
    {
        await using var source = Source();
        await new PostgresMigrationRunner(source).MigrateAsync();
        var artifact = await new PostgresKnowledgeReleaseStore(source).RegisterAsync(Fixture("c"));
        var store = new PostgresReviewedKnowledgeActivationStore(source);
        var wrong = new KnowledgeChangeProposal(Id(), artifact.PackId, artifact.ReleaseId, new string('0',64), "source", "impact", "tests", "proposer", Time);
        await Assert.ThrowsAsync<KnowledgeGovernanceConflictException>(() => store.ProposeAsync(wrong));
        Assert.Null(await store.LoadChangeAsync(wrong.ChangeId));
        var rejected = Proposal(artifact);
        await store.ProposeAsync(rejected);
        await Assert.ThrowsAsync<KnowledgeGovernanceConflictException>(() => store.ProposeAsync(rejected));
        await store.DecideAsync(new(rejected.ChangeId, "reviewer", Time, false, "unresolved tests"));
        await Assert.ThrowsAsync<KnowledgeGovernanceConflictException>(() => store.DecideAsync(new(rejected.ChangeId, "other", Time, true, "cannot replace")));
        await Assert.ThrowsAsync<KnowledgeGovernanceConflictException>(() => store.ActivateAsync(new(rejected.ChangeId, 0, "activator", Time)));
        Assert.Null(await store.LoadActiveAsync(artifact.PackId));
        var proposals = new[] { Proposal(artifact), Proposal(artifact) };
        foreach (var proposal in proposals)
        {
            await store.ProposeAsync(proposal);
            await store.DecideAsync(new(proposal.ChangeId, "reviewer", Time, true, "synthetic only"));
        }
        async Task<bool> Attempt(KnowledgeChangeProposal proposal)
        {
            try { await store.ActivateAsync(new(proposal.ChangeId, 0, "activator", Time)); return true; }
            catch (KnowledgeGovernanceConflictException) { return false; }
        }
        var results = await Task.WhenAll(proposals.Select(Attempt));
        Assert.Single(results, result => result);
        Assert.Single(results, result => !result);
        Assert.Equal(1, (await store.LoadActiveAsync(artifact.PackId))!.Revision);
        Assert.Null(await store.LoadActivationAsync(artifact.PackId, 2));
    }

    private static KnowledgeChangeProposal Proposal(KnowledgeReleaseArtifact artifact)
        => new(Id(), artifact.PackId, artifact.ReleaseId, artifact.Sha256, "source:synthetic", "impact:synthetic", "tests:synthetic", "proposer", Time);
    private static string Id() => "synthetic-" + Guid.NewGuid().ToString("N");
    private static string Fixture(string letter)
    {
        var node = JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures/demo-" + letter + "-pack.json")))!;
        node["manifest"]!["packId"] = Id();
        node["manifest"]!["releaseId"] = Id();
        return node.ToJsonString();
    }
    private static NpgsqlDataSource Source() => NpgsqlDataSource.Create(
        Environment.GetEnvironmentVariable("NORMACASE_POSTGRES_TEST_CONNECTION")
        ?? throw new InvalidOperationException("Synthetic test database required."));
}
