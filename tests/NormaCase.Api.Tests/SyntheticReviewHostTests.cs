using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace NormaCase.Api.Tests;

public sealed class SyntheticReviewHostTests
{
    private const string Credential = "AAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh8=";

    [Fact]
    public async Task Readiness_tracks_the_current_persistent_store_without_exposing_failure_details()
    {
        var connection = Environment.GetEnvironmentVariable("NORMACASE_POSTGRES_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(connection)) return;
        await ResetDatabase(connection);
        await using var host = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["SyntheticReview:Enabled"] = "true",
                    ["SyntheticReview:PersistenceEnabled"] = "true",
                    ["ConnectionStrings:SyntheticReview"] = connection,
                    ["SyntheticReview:Credential"] = Credential
                })));
        using var client = host.CreateClient();

        var ready = await client.GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.OK, ready.StatusCode);
        using (var readyJson = JsonDocument.Parse(await ready.Content.ReadAsStringAsync()))
        {
            Assert.Equal("bereit", readyJson.RootElement.GetProperty("status").GetString());
            Assert.Equal("verfügbar", readyJson.RootElement.GetProperty("persistence").GetString());
        }

        await host.Services.GetRequiredService<NpgsqlDataSource>().DisposeAsync();
        var unavailable = await client.GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, unavailable.StatusCode);
        var body = await unavailable.Content.ReadAsStringAsync();
        using var unavailableJson = JsonDocument.Parse(body);
        Assert.Equal("nicht bereit", unavailableJson.RootElement.GetProperty("status").GetString());
        Assert.Equal("nicht verfügbar", unavailableJson.RootElement.GetProperty("persistence").GetString());
        Assert.Equal(2, unavailableJson.RootElement.EnumerateObject().Count());
        Assert.DoesNotContain("Host=", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Npgsql", body, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("no-store", unavailable.Headers.CacheControl!.ToString());
    }

    [Fact]
    public async Task Distinct_identities_filter_cases_actions_and_preserve_actual_review_actor()
    {
        var connection = Environment.GetEnvironmentVariable("NORMACASE_POSTGRES_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(connection)) return;
        await ResetDatabase(connection);
        var second = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
        await using var host = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["SyntheticReview:Enabled"] = "true",
                    ["SyntheticReview:PersistenceEnabled"] = "true",
                    ["ConnectionStrings:SyntheticReview"] = connection,
                    ["SyntheticReview:Credential"] = null,
                    ["SyntheticReview:Users:alice:Credential"] = Credential,
                    ["SyntheticReview:Users:alice:Actions:0"] = "READ",
                    ["SyntheticReview:Users:alice:Actions:1"] = "ACCEPT",
                    ["SyntheticReview:Users:alice:CaseIds:0"] = "demo-g-supported",
                    ["SyntheticReview:Users:bob:Credential"] = second,
                    ["SyntheticReview:Users:bob:Actions:0"] = "READ",
                    ["SyntheticReview:Users:bob:CaseIds:0"] = "demo-g-not-supported"
                })));
        using var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Credential);
        using var queues = JsonDocument.Parse(await client.GetStringAsync("/api/review/work-queues"));
        var cases = queues.RootElement.GetProperty("queues").EnumerateArray()
            .SelectMany(queue => queue.GetProperty("items").EnumerateArray())
            .Select(item => item.GetProperty("caseId").GetString()).ToArray();
        Assert.Equal(new[] { "demo-g-supported" }, cases);
        using var scopedPage = JsonDocument.Parse(await client.GetStringAsync("/api/review/work-queues?pageSize=1"));
        Assert.Equal(JsonValueKind.Null, scopedPage.RootElement.GetProperty("nextPageCursor").ValueKind);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/review/work-queues?pageSize=101")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/review/work-queues?afterCaseId=invalid")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/review/work-cases/demo-g-not-supported")).StatusCode);
        using var detail = JsonDocument.Parse(await client.GetStringAsync("/api/review/work-cases/demo-g-supported"));
        Assert.Equal(new[] { "ACCEPT_SYSTEM_RESULT" }, detail.RootElement.GetProperty("allowedActions").EnumerateArray().Select(item => item.GetString()));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/review/work-cases/demo-g-supported/outbound", new { })).StatusCode);
        var deniedOverride = await client.PostAsJsonAsync("/api/review/work-cases/demo-g-supported/reviews", new
        {
            expectedCaseRevision = "1", expectedProcessRevision = "1", expectedAuditRevision = "1",
            disposition = "OVERRIDE", reason = "Synthetischer nicht erlaubter Override", overrideOutcome = "NOT_SUPPORTED"
        });
        Assert.Equal(HttpStatusCode.Forbidden, deniedOverride.StatusCode);
        var accepted = await client.PostAsJsonAsync("/api/review/work-cases/demo-g-supported/reviews", new
        {
            expectedCaseRevision = "1", expectedProcessRevision = "1", expectedAuditRevision = "1",
            disposition = "ACCEPT_SYSTEM_RESULT", reason = "Synthetische individuelle Freigabe"
        });
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        using var result = JsonDocument.Parse(await accepted.Content.ReadAsStringAsync());
        Assert.Equal("synthetic-local:user-alice", result.RootElement.GetProperty("audit").EnumerateArray().Last().GetProperty("actorId").GetString());
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", second);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/review/work-cases/demo-g-supported")).StatusCode);
        using var bob = JsonDocument.Parse(await client.GetStringAsync("/api/review/work-cases/demo-g-not-supported"));
        Assert.Empty(bob.RootElement.GetProperty("allowedActions").EnumerateArray());
        var deniedReview = await client.PostAsJsonAsync("/api/review/work-cases/demo-g-not-supported/reviews", new
        {
            expectedCaseRevision = "1", expectedProcessRevision = "1", expectedAuditRevision = "1",
            disposition = "ACCEPT_SYSTEM_RESULT", reason = "Synthetischer nicht erlaubter Review"
        });
        Assert.Equal(HttpStatusCode.Forbidden, deniedReview.StatusCode);
    }

    [Fact]
    public async Task Authenticated_review_host_persists_authorized_reviews_and_fails_closed()
    {
        var connection = Environment.GetEnvironmentVariable("NORMACASE_POSTGRES_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(connection))
            return;

        await ResetDatabase(connection);

        await using (var factory = Factory(connection))
        {
            using var anonymous = factory.CreateClient();
            var unauthorized = await anonymous.GetAsync("/api/review/work-queues");
            Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);
            Assert.Contains("Anmeldung", await unauthorized.Content.ReadAsStringAsync(), StringComparison.Ordinal);

            using var wrong = factory.CreateClient();
            wrong.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Convert.ToBase64String(new byte[32]));
            Assert.Equal(HttpStatusCode.Unauthorized, (await wrong.GetAsync("/api/review/work-queues")).StatusCode);

            using var client = factory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Credential);
            await using (var knowledge = NpgsqlDataSource.Create(connection))
            {
                await using var count = knowledge.CreateCommand("SELECT count(*) FROM normacase.knowledge_release_artifacts");
                Assert.Equal(7L, await count.ExecuteScalarAsync());
                var retained = await new NormaCase.Persistence.PostgreSql.PostgresKnowledgeReleaseStore(knowledge)
                    .LoadAsync("synthetic.demo-g", "demo-g-2026.1");
                Assert.NotNull(retained);
                Assert.Equal("SYNTHETIC", retained.ValidationLevel);
            }
            var seen = new List<string>();
            string? cursor = null;
            do
            {
                using var page = JsonDocument.Parse(await client.GetStringAsync("/api/review/work-queues?pageSize=1"
                    + (cursor is null ? "" : "&afterCaseId=" + Uri.EscapeDataString(cursor))));
                seen.AddRange(page.RootElement.GetProperty("queues").EnumerateArray()
                    .SelectMany(queue => queue.GetProperty("items").EnumerateArray())
                    .Select(item => item.GetProperty("caseId").GetString()!));
                cursor = page.RootElement.GetProperty("nextPageCursor").GetString();
                Assert.True(seen.Count <= 4);
            } while (cursor is not null);
            Assert.Equal(new[] { "demo-g-incomplete", "demo-g-not-supported", "demo-g-review", "demo-g-supported" }, seen);
            using var queues = JsonDocument.Parse(await client.GetStringAsync("/api/review/work-queues"));
            Assert.Equal(new[] { 2, 1, 1, 0 },
                queues.RootElement.GetProperty("queues").EnumerateArray()
                    .Select(item => item.GetProperty("items").GetArrayLength()).ToArray());

            using var incomplete = JsonDocument.Parse(await client.GetStringAsync("/api/review/work-cases/demo-g-incomplete"));
            Assert.Empty(incomplete.RootElement.GetProperty("allowedActions").EnumerateArray());
            var denied = await client.PostAsJsonAsync("/api/review/work-cases/demo-g-incomplete/reviews",
                new
                {
                    expectedCaseRevision = "1",
                    expectedProcessRevision = "1",
                    expectedAuditRevision = "1",
                    disposition = "ACCEPT_SYSTEM_RESULT",
                    reason = "Synthetischer unzulässiger Versuch"
                });
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);

            var stale = await client.PostAsJsonAsync("/api/review/work-cases/demo-g-supported/reviews",
                new
                {
                    expectedCaseRevision = "1",
                    expectedProcessRevision = "0",
                    expectedAuditRevision = "1",
                    disposition = "ACCEPT_SYSTEM_RESULT",
                    reason = "Veralteter synthetischer Versuch"
                });
            Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);

            var accepted = await client.PostAsJsonAsync("/api/review/work-cases/demo-g-supported/reviews",
                new
                {
                    expectedCaseRevision = "1",
                    expectedProcessRevision = "1",
                    expectedAuditRevision = "1",
                    disposition = "ACCEPT_SYSTEM_RESULT",
                    reason = "Synthetische Freigabe"
                });
            Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
            using var acceptedJson = JsonDocument.Parse(await accepted.Content.ReadAsStringAsync());
            Assert.Equal("accepted", acceptedJson.RootElement.GetProperty("stateId").GetString());
            Assert.Equal("2", acceptedJson.RootElement.GetProperty("processRevision").GetString());
            var acceptedAudit = acceptedJson.RootElement.GetProperty("audit").EnumerateArray().ToArray();
            Assert.Equal(2, acceptedAudit.Length);
            Assert.Equal("synthetic-local:reviewer", acceptedAudit[1].GetProperty("actorId").GetString());
            Assert.Equal("ACCEPT_SYSTEM_RESULT", acceptedAudit[1].GetProperty("disposition").GetString());

            var repeated = await client.PostAsJsonAsync("/api/review/work-cases/demo-g-supported/reviews",
                new
                {
                    expectedCaseRevision = "1",
                    expectedProcessRevision = "1",
                    expectedAuditRevision = "1",
                    disposition = "ACCEPT_SYSTEM_RESULT",
                    reason = "Synthetischer Wiederholungsversuch"
                });
            Assert.Equal(HttpStatusCode.Conflict, repeated.StatusCode);
            var closedState = await client.PostAsJsonAsync("/api/review/work-cases/demo-g-supported/reviews",
                new
                {
                    expectedCaseRevision = "1",
                    expectedProcessRevision = "2",
                    expectedAuditRevision = "2",
                    disposition = "ACCEPT_SYSTEM_RESULT",
                    reason = "Unzulässiger abgeschlossener Übergang"
                });
            Assert.Equal(HttpStatusCode.Forbidden, closedState.StatusCode);

            var overridden = await client.PostAsJsonAsync("/api/review/work-cases/demo-g-not-supported/reviews",
                new
                {
                    expectedCaseRevision = "1",
                    expectedProcessRevision = "1",
                    expectedAuditRevision = "1",
                    disposition = "OVERRIDE",
                    reason = "Synthetische Abweichung",
                    overrideOutcome = "SUPPORTED"
                });
            Assert.Equal(HttpStatusCode.OK, overridden.StatusCode);
            using var overrideJson = JsonDocument.Parse(await overridden.Content.ReadAsStringAsync());
            Assert.Equal("overridden", overrideJson.RootElement.GetProperty("stateId").GetString());
            Assert.Equal("SUPPORTED", overrideJson.RootElement.GetProperty("audit").EnumerateArray().Last()
                .GetProperty("overrideOutcome").GetString());

            using var refreshed = JsonDocument.Parse(await client.GetStringAsync("/api/review/work-queues"));
            Assert.Equal(2, refreshed.RootElement.GetProperty("queues").EnumerateArray().Last()
                .GetProperty("items").GetArrayLength());
        }

        await using var restarted = Factory(connection);
        using var afterRestart = restarted.CreateClient();
        afterRestart.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Credential);
        using var persisted = JsonDocument.Parse(await afterRestart.GetStringAsync("/api/review/work-cases/demo-g-supported"));
        Assert.Equal("accepted", persisted.RootElement.GetProperty("stateId").GetString());
        Assert.Equal(2, persisted.RootElement.GetProperty("audit").GetArrayLength());
    }

    [Fact]
    public async Task Fresh_intake_review_and_two_outbound_sinks_survive_restart()
    {
        var connection = Environment.GetEnvironmentVariable("NORMACASE_POSTGRES_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(connection)) return;
        await ResetDatabase(connection);
        var directory = Path.Combine(Path.GetTempPath(), "normacase-roundtrip-" + Guid.NewGuid().ToString("N"));
        var order = "roundtrip-" + Guid.NewGuid().ToString("N");
        using var input = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Cases", "demo-g-supported.json")));
        var payload = new { formatVersion = 1, order, message = "input-message", revision = "1", input = input.RootElement };
        string caseId;
        string originalOutbound;
        try
        {
            await using (var host = Factory(connection, directory))
            {
                using var client = host.CreateClient();
                Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/review/intake/json", payload)).StatusCode);
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Credential);
                var accepted = await client.PostAsJsonAsync("/api/review/intake/json", payload);
                Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
                using var json = JsonDocument.Parse(await accepted.Content.ReadAsStringAsync());
                caseId = json.RootElement.GetProperty("workCase").GetProperty("caseId").GetString()!;
                Assert.Equal("ACCEPTED", json.RootElement.GetProperty("acceptance").GetString());
                Assert.Equal("awaiting-approval", json.RootElement.GetProperty("workCase").GetProperty("stateId").GetString());
                var duplicate = await client.PostAsJsonAsync("/api/review/intake/json", payload);
                using var duplicateJson = JsonDocument.Parse(await duplicate.Content.ReadAsStringAsync());
                Assert.Equal("DUPLICATE", duplicateJson.RootElement.GetProperty("acceptance").GetString());
                using var queues = JsonDocument.Parse(await client.GetStringAsync("/api/review/work-queues"));
                Assert.Contains(queues.RootElement.GetProperty("queues")[0].GetProperty("items").EnumerateArray(), item => item.GetProperty("caseId").GetString() == caseId);
                var export = new { messageId = "result-message", correlationId = "synthetic-correlation", destinationId = "synthetic-inbox", expectedCaseRevision = "1", expectedProcessRevision = "1", expectedAuditRevision = "1" };
                Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync($"/api/review/work-cases/{caseId}/outbound", export)).StatusCode);
                Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync($"/api/review/work-cases/{caseId}/reviews", new { expectedCaseRevision = "1", expectedProcessRevision = "1", expectedAuditRevision = "1", disposition = "ACCEPT_SYSTEM_RESULT", reason = "Synthetischer Ende-zu-Ende-Test" })).StatusCode);
                Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync($"/api/review/work-cases/{caseId}/outbound", export)).StatusCode);
                var delivered = await client.PostAsJsonAsync($"/api/review/work-cases/{caseId}/outbound", export with { expectedProcessRevision = "2", expectedAuditRevision = "2" });
                Assert.Equal(HttpStatusCode.OK, delivered.StatusCode);
                using var deliveredJson = JsonDocument.Parse(await delivered.Content.ReadAsStringAsync());
                originalOutbound = deliveredJson.RootElement.GetProperty("resultJson").GetString()!;
                using var result = JsonDocument.Parse(originalOutbound);
                Assert.Equal("synthetic-correlation", result.RootElement.GetProperty("result").GetProperty("correlationId").GetString());
                Assert.Equal("input-message", result.RootElement.GetProperty("result").GetProperty("upstreamMessageId").GetString());
                Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync($"/api/review/work-cases/{caseId}/outbound", export with { correlationId = "changed", expectedProcessRevision = "2", expectedAuditRevision = "2" })).StatusCode);
                Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync($"/api/review/work-cases/{caseId}/outbound", export with { destinationId = "synthetic-file", expectedProcessRevision = "2", expectedAuditRevision = "2" })).StatusCode);
                Assert.Single(Directory.GetFiles(directory, "*.json"));
                Assert.Equal(originalOutbound, await File.ReadAllTextAsync(Directory.GetFiles(directory, "*.json").Single()));
                var xml = $"<SyntheticCase formatVersion=\"1\" order=\"xml-{order}\" message=\"xml-message\" revision=\"1\" date=\"2026-10-03\"><Fact id=\"request_complete\" kind=\"TRUTH\" value=\"YES\"/><Fact id=\"criteria_confirmed\" kind=\"TRUTH\" value=\"YES\"/><Evidence id=\"supporting_document\" value=\"PRESENT\"/></SyntheticCase>";
                Assert.Equal(HttpStatusCode.OK, (await client.PostAsync("/api/review/intake/xml", new StringContent(xml, System.Text.Encoding.UTF8, "application/xml"))).StatusCode);
                Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/review/intake/xml", new StringContent("<!DOCTYPE x [<!ENTITY ext SYSTEM 'file:///etc/passwd'>]><SyntheticCase/>", System.Text.Encoding.UTF8, "application/xml"))).StatusCode);
                Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/review/intake/json", payload with { revision = "2", message = "new-revision" })).StatusCode);
            }
            await using var restarted = Factory(connection, directory);
            using var after = restarted.CreateClient();
            after.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Credential);
            using var historical = JsonDocument.Parse(await (await after.PostAsJsonAsync("/api/review/intake/json", payload)).Content.ReadAsStringAsync());
            Assert.Equal("accepted", historical.RootElement.GetProperty("workCase").GetProperty("stateId").GetString());
            foreach (var destinationId in new[] { "synthetic-inbox", "synthetic-file" })
            {
                var receipt = await after.PostAsJsonAsync($"/api/review/work-cases/{caseId}/outbound", new { messageId = "result-message", correlationId = "synthetic-correlation", destinationId, expectedCaseRevision = "1", expectedProcessRevision = "2", expectedAuditRevision = "2" });
                Assert.Equal(HttpStatusCode.OK, receipt.StatusCode);
                using var restored = JsonDocument.Parse(await receipt.Content.ReadAsStringAsync());
                Assert.Equal(originalOutbound, restored.RootElement.GetProperty("resultJson").GetString());
            }
            Assert.Single(Directory.GetFiles(directory, "*.json"));
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [Fact]
    public void Persistent_review_requires_verified_authentication()
    {
        using var host = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("SyntheticReview:PersistenceEnabled", "true");
            builder.UseSetting("ConnectionStrings:SyntheticReview", "Host=127.0.0.1;Database=unused");
        });
        Assert.ThrowsAny<Exception>(() => host.CreateClient());
    }

    [Fact]
    public void Persistent_review_requires_postgresql_configuration()
    {
        using var host = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("SyntheticReview:Enabled", "true");
            builder.UseSetting("SyntheticReview:PersistenceEnabled", "true");
            builder.UseSetting("SyntheticReview:Credential", Credential);
        });
        Assert.ThrowsAny<Exception>(() => host.CreateClient());
    }

    private static WebApplicationFactory<Program> Factory(string connection, string? outboundDirectory = null)
        => new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("SyntheticReview:Enabled", "true");
            builder.UseSetting("SyntheticReview:PersistenceEnabled", "true");
            builder.UseSetting("SyntheticReview:Credential", Credential);
            builder.UseSetting("ConnectionStrings:SyntheticReview", connection);
            if (outboundDirectory is not null) builder.UseSetting("SyntheticReview:OutboundDirectory", outboundDirectory);
        });

    private static async Task ResetDatabase(string connection)
    {
        await using var source = NpgsqlDataSource.Create(connection);
        await using var command = source.CreateCommand("DROP SCHEMA IF EXISTS normacase CASCADE");
        await command.ExecuteNonQueryAsync();
    }
}
