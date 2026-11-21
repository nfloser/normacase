using System.Text.Json.Nodes;
using Npgsql;
using NpgsqlTypes;
using NormaCase.Knowledge.Catalog;
using NormaCase.Application.Authorization;
using NormaCase.Persistence.PostgreSql;
using Xunit;

namespace NormaCase.Persistence.PostgreSql.Tests;

public sealed class PostgresKnowledgeReleaseStoreTests
{
    [Fact]
    public async Task Release_pages_and_import_audit_are_exact_bounded_and_restart_safe()
    {
        await using var source = Source();
        await new PostgresMigrationRunner(source).MigrateAsync();
        var releases = new PostgresKnowledgeReleaseStore(source);
        var imports = new PostgresKnowledgeReleaseImportStore(source);
        var actor = "synthetic-local:import-" + Guid.NewGuid().ToString("N");
        var entitlements = new PostgresReviewedEntitlementChangeStore(source);
        await entitlements.ReconcileBaselineAsync(new(actor, 0, [], []));
        var node = JsonNode.Parse(Fixture("c"))!;
        node["manifest"]!["packId"] = "import-page-" + Guid.NewGuid().ToString("N");
        var packId = node["manifest"]!["packId"]!.GetValue<string>();
        async Task<KnowledgeReleaseImportRecord> Import(string json) => await entitlements.ExecuteWithEffectiveLockAsync(actor,
            (_, token) => imports.ImportAsync(json, DateTimeOffset.UnixEpoch, token));
        node["manifest"]!["releaseId"] = "release-a";
        var jsonA = node.ToJsonString() + "\n  ";
        var first = await Import(jsonA);
        Assert.Equal(jsonA, first.Artifact.KnowledgePackJson);
        Assert.Equal(actor, first.ImportedByActorId);
        Assert.Equal(DateTimeOffset.UnixEpoch, first.ImportedAtUtc);
        var duplicates = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => Import(jsonA)));
        Assert.All(duplicates, item => Assert.Equal(first.ImportedByActorId, item.ImportedByActorId));
        var laterActor = "synthetic-local:later-import-" + Guid.NewGuid().ToString("N");
        await entitlements.ReconcileBaselineAsync(new(laterActor, 0, [], []));
        var repeated = await entitlements.ExecuteWithEffectiveLockAsync(laterActor,
            (_, token) => imports.ImportAsync(jsonA, DateTimeOffset.UnixEpoch.AddDays(1), token));
        Assert.Equal(actor, repeated.ImportedByActorId);
        Assert.Equal(DateTimeOffset.UnixEpoch, repeated.ImportedAtUtc);
        await Assert.ThrowsAsync<KnowledgeReleaseIdentityConflictException>(() => Import(jsonA + " "));
        node["manifest"]!["releaseId"] = "release-b";
        await Import(node.ToJsonString());
        var page = await releases.ListAsync(1, packId: packId, validationLevel: "SYNTHETIC");
        Assert.Equal(new[] { "release-a", "release-b" }, page.Select(item => item.ReleaseId));
        var next = await releases.ListAsync(1, packId: packId, afterPackId: packId, afterReleaseId: "release-a", validationLevel: "SYNTHETIC");
        Assert.Equal(new[] { "release-b" }, next.Select(item => item.ReleaseId));
        Assert.Empty(await releases.ListAsync(1, packId: packId, afterPackId: packId, afterReleaseId: "release-b"));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => releases.ListAsync(101));
        await Assert.ThrowsAsync<ArgumentException>(() => releases.ListAsync(1, afterPackId: packId));
        await using var restartedSource = Source();
        var retained = (await new PostgresKnowledgeReleaseImportStore(restartedSource).LoadAsync(packId, first.Artifact.ReleaseId))!;
        Assert.Equal(actor, retained.ImportedByActorId);
        Assert.Equal(jsonA, retained.Artifact.KnowledgePackJson);
        foreach (var sql in new[] {
            "UPDATE normacase.knowledge_release_imports SET actor_id=actor_id WHERE pack_id=$1",
            "DELETE FROM normacase.knowledge_release_imports WHERE pack_id=$1" })
        {
            await using var command = source.CreateCommand(sql);
            command.Parameters.AddWithValue(packId);
            await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
        }
    }

    [Fact]
    public async Task Concurrent_registration_is_exact_idempotent_and_restart_preserves_original_json()
    {
        await using var source = Source();
        await new PostgresMigrationRunner(source).MigrateAsync();
        var json = Fixture("c") + "\n  ";
        var store = new PostgresKnowledgeReleaseStore(source);
        var results = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => store.RegisterAsync(json)));
        Assert.All(results, artifact => Assert.Equal(json, artifact.KnowledgePackJson));
        var first = results[0];
        await using var restarted = Source();
        var retained = (await new PostgresKnowledgeReleaseStore(restarted).LoadAsync(first.PackId, first.ReleaseId))!;
        Assert.Equal(json, retained.KnowledgePackJson);
        Assert.Equal(first.Sha256, retained.Sha256);
        Assert.Equal("SYNTHETIC", retained.ValidationLevel);
        await Assert.ThrowsAsync<KnowledgeReleaseIdentityConflictException>(() => store.RegisterAsync(json + " "));
        Assert.Null(await store.LoadAsync(first.PackId, "missing-release"));
        retained.LoadPack().Fields.Clear();
        Assert.NotEmpty(retained.LoadPack().Fields);
    }

    [Fact]
    public async Task Distinct_packs_and_historical_versions_are_exact_without_latest_selection()
    {
        await using var source = Source();
        await new PostgresMigrationRunner(source).MigrateAsync();
        var store = new PostgresKnowledgeReleaseStore(source);
        var a = await store.RegisterAsync(Fixture("a"));
        var c = await store.RegisterAsync(Fixture("c"));
        var node = JsonNode.Parse(a.KnowledgePackJson)!;
        node["manifest"]!["releaseId"] = "synthetic-" + Guid.NewGuid().ToString("N");
        var newer = await store.RegisterAsync(node.ToJsonString());
        Assert.NotEqual(a.PackId, c.PackId);
        Assert.NotEqual(a.ReleaseId, newer.ReleaseId);
        Assert.Equal(a.KnowledgePackJson, (await store.LoadAsync(a.PackId, a.ReleaseId))!.KnowledgePackJson);
        Assert.Equal(c.KnowledgePackJson, (await store.LoadAsync(c.PackId, c.ReleaseId))!.KnowledgePackJson);
        Assert.Null(await store.LoadAsync(a.PackId, "latest"));
    }

    [Fact]
    public async Task Database_rejects_update_delete_and_loader_rejects_false_metadata()
    {
        await using var source = Source();
        await new PostgresMigrationRunner(source).MigrateAsync();
        var store = new PostgresKnowledgeReleaseStore(source);
        var artifact = await store.RegisterAsync(Fixture("c"));
        await using var connection = await source.OpenConnectionAsync();
        foreach (var sql in new[] {
            "UPDATE normacase.knowledge_release_artifacts SET validation_level=validation_level WHERE pack_id=$1 AND release_id=$2",
            "DELETE FROM normacase.knowledge_release_artifacts WHERE pack_id=$1 AND release_id=$2" })
        {
            await using var mutation = new NpgsqlCommand(sql, connection);
            mutation.Parameters.AddWithValue(artifact.PackId); mutation.Parameters.AddWithValue(artifact.ReleaseId);
            await Assert.ThrowsAsync<PostgresException>(() => mutation.ExecuteNonQueryAsync());
        }
        var original = new KnowledgeReleaseCatalog().Register(Fixture("a"));
        await using var forged = new NpgsqlCommand("""
            INSERT INTO normacase.knowledge_release_artifacts
            (pack_id,release_id,lifecycle_status,validation_level,pack_json,pack_sha256)
            VALUES ($1,$2,$3,'PRODUCTION_APPROVED',$4,$5)
            """, connection);
        forged.Parameters.AddWithValue(original.PackId); forged.Parameters.AddWithValue(original.ReleaseId);
        forged.Parameters.AddWithValue(original.LifecycleStatus);
        forged.Parameters.AddWithValue(NpgsqlDbType.Json, original.KnowledgePackJson);
        forged.Parameters.AddWithValue(original.Sha256);
        await forged.ExecuteNonQueryAsync();
        await Assert.ThrowsAsync<KnowledgeReleaseIntegrityException>(() => store.LoadAsync(original.PackId, original.ReleaseId));
    }

    private static string Fixture(string letter)
    {
        var node = JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures/demo-" + letter + "-pack.json")))!;
        node["manifest"]!["releaseId"] = "synthetic-" + Guid.NewGuid().ToString("N");
        return node.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
    }
    private static NpgsqlDataSource Source() => NpgsqlDataSource.Create(
        Environment.GetEnvironmentVariable("NORMACASE_POSTGRES_TEST_CONNECTION")
        ?? throw new InvalidOperationException("Synthetic test database required."));
}
