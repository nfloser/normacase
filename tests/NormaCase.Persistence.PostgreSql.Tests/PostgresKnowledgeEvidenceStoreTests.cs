using System.Text.Json.Nodes;
using Npgsql;
using NormaCase.Application.Knowledge;
using Xunit;

namespace NormaCase.Persistence.PostgreSql.Tests;

public sealed class PostgresKnowledgeEvidenceStoreTests
{
    [Fact]
    public async Task Exact_evidence_is_idempotent_retained_after_restart_and_immutable()
    {
        await using var source = Source();
        await new PostgresMigrationRunner(source).MigrateAsync();
        var store = new PostgresKnowledgeEvidenceStore(source);
        var artifact = Evidence("SOURCE", "Synthetische Quelle\r\n  äöü\t\n");
        var copies = await Task.WhenAll(Enumerable.Range(0,6).Select(_ => store.RegisterAsync(artifact)));
        Assert.All(copies, copy => Assert.Equal(artifact, copy));
        await using var restarted = Source();
        Assert.Equal(artifact, await new PostgresKnowledgeEvidenceStore(restarted).LoadAsync(artifact.EvidenceId));
        await Assert.ThrowsAsync<KnowledgeGovernanceConflictException>(() => store.RegisterAsync(new(artifact.EvidenceId,
            artifact.Kind, artifact.Title, artifact.Content + " ", artifact.RecordedByActorId, artifact.RecordedAtUtc)));
        foreach (var sql in new[] { "UPDATE normacase.knowledge_evidence_artifacts SET content=content WHERE evidence_id=$1", "DELETE FROM normacase.knowledge_evidence_artifacts WHERE evidence_id=$1" })
        {
            await using var mutation = source.CreateCommand(sql);
            mutation.Parameters.AddWithValue(artifact.EvidenceId);
            await Assert.ThrowsAsync<PostgresException>(() => mutation.ExecuteNonQueryAsync());
        }
        var id = Id();
        await using var forged = source.CreateCommand("""
            INSERT INTO normacase.knowledge_evidence_artifacts
            (evidence_id,kind,title,content,content_sha256,recorded_by_actor_id,recorded_at_utc)
            VALUES ($1,'SOURCE','Synthetisch','changed text',$2,'synthetic-forged',$3)
            """);
        forged.Parameters.AddWithValue(id); forged.Parameters.AddWithValue(artifact.Sha256);
        forged.Parameters.AddWithValue(DateTimeOffset.UnixEpoch);
        await forged.ExecuteNonQueryAsync();
        await Assert.ThrowsAsync<KnowledgeEvidenceIntegrityException>(() => store.LoadAsync(id));
    }

    [Fact]
    public async Task Strict_governance_requires_typed_verified_evidence_and_keeps_legacy_history_readable()
    {
        await using var source = Source();
        await new PostgresMigrationRunner(source).MigrateAsync();
        var node = JsonNode.Parse(File.ReadAllText(System.IO.Path.Combine(AppContext.BaseDirectory,"Fixtures/demo-c-pack.json")))!;
        node["manifest"]!["packId"] = Id(); node["manifest"]!["releaseId"] = Id();
        var pack = await new PostgresKnowledgeReleaseStore(source).RegisterAsync(node.ToJsonString());
        var evidenceStore = new PostgresKnowledgeEvidenceStore(source);
        var evidence = new[] { Evidence("SOURCE", "Synthetische Quelle"), Evidence("IMPACT", "Synthetische Analyse"), Evidence("TESTS", "Synthetische Testergebnisse") };
        var time = DateTimeOffset.UnixEpoch.AddSeconds(1);
        KnowledgeChangeProposal Proposal(string sourceId, string impactId, string testsId)
            => new(Id(),pack.PackId,pack.ReleaseId,pack.Sha256,sourceId,impactId,testsId,"proposer",time);
        var strict = new PostgresReviewedKnowledgeActivationStore(source,requireRetainedEvidence:true);
        var missing = Proposal(evidence[0].EvidenceId,evidence[1].EvidenceId,evidence[2].EvidenceId);
        await Assert.ThrowsAsync<KnowledgeGovernanceConflictException>(() => strict.ProposeAsync(missing));
        Assert.Null(await strict.LoadChangeAsync(missing.ChangeId));
        var legacy = new PostgresReviewedKnowledgeActivationStore(source);
        await legacy.ProposeAsync(missing);
        Assert.NotNull(await strict.LoadChangeAsync(missing.ChangeId));
        await Assert.ThrowsAsync<KnowledgeGovernanceConflictException>(() => strict.DecideAsync(new(missing.ChangeId,"reviewer",time,true,"synthetic")));
        Assert.Null((await strict.LoadChangeAsync(missing.ChangeId))!.Decision);
        foreach (var item in evidence) await evidenceStore.RegisterAsync(item);
        var wrong = Proposal(evidence[2].EvidenceId,evidence[1].EvidenceId,evidence[0].EvidenceId);
        await Assert.ThrowsAsync<KnowledgeGovernanceConflictException>(() => strict.ProposeAsync(wrong));
        var proposal = Proposal(evidence[0].EvidenceId,evidence[1].EvidenceId,evidence[2].EvidenceId);
        await strict.ProposeAsync(proposal);
        await strict.DecideAsync(new(proposal.ChangeId,"reviewer",time,true,"synthetic tests checked"));
        var activated = await strict.ActivateAsync(new(proposal.ChangeId,0,"activator",time));
        Assert.Equal(pack.Sha256,activated.Sha256);
        Assert.Equal("SYNTHETIC",(await new PostgresKnowledgeReleaseStore(source).LoadAsync(pack.PackId,pack.ReleaseId))!.ValidationLevel);
        // Evidence created after an old proposal cannot silently repair its original provenance.
        var late = new KnowledgeEvidenceArtifact(Id(),"SOURCE","Late", "synthetic late evidence","proposer",time.AddSeconds(1));
        await evidenceStore.RegisterAsync(late);
        await Assert.ThrowsAsync<KnowledgeGovernanceConflictException>(() => strict.ProposeAsync(Proposal(late.EvidenceId,evidence[1].EvidenceId,evidence[2].EvidenceId)));
    }
    private static KnowledgeEvidenceArtifact Evidence(string kind,string content)
        => new(Id(),kind,"Synthetischer Nachweis",content,"proposer",DateTimeOffset.UnixEpoch);
    private static string Id() => "synthetic-" + Guid.NewGuid().ToString("N");
    private static NpgsqlDataSource Source() => NpgsqlDataSource.Create(Environment.GetEnvironmentVariable("NORMACASE_POSTGRES_TEST_CONNECTION")
        ?? throw new InvalidOperationException("Synthetic test database required."));
}
