using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;
using Xunit;

namespace NormaCase.Api.Tests;

public sealed class SyntheticEntitlementAdministrationTests
{
    private static readonly string Alice = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
    private static readonly string Administrator = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
    private static readonly string Approver = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    [Fact]
    public async Task Administrator_proposes_and_distinct_approver_decides_a_durable_entitlement_change()
    {
        var connection = Environment.GetEnvironmentVariable("NORMACASE_POSTGRES_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(connection)) return;
        await ResetDatabase(connection);

        string changeId;
        await using (var host = Factory(connection))
        {
            using var administrator = Client(host, Administrator);
            using var approver = Client(host, Approver);
            using var alice = Client(host, Alice);

            Assert.Equal(HttpStatusCode.Forbidden,
                (await alice.GetAsync("/api/review/administration/entitlement-changes/pending")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden,
                (await approver.GetAsync("/api/review/administration/identities")).StatusCode);

            using var catalog = JsonDocument.Parse(
                await administrator.GetStringAsync("/api/review/administration/entitlement-changes/catalog"));
            Assert.Contains("READ", catalog.RootElement.GetProperty("actions").EnumerateArray()
                .Select(item => item.GetString()));
            Assert.Contains("demo-g-supported", catalog.RootElement.GetProperty("caseIds").EnumerateArray()
                .Select(item => item.GetString()));

            var invalidTarget = await administrator.PostAsJsonAsync(
                "/api/review/administration/entitlement-changes",
                new
                {
                    targetActorId = "synthetic-local:user-unknown",
                    expectedEntitlementRevision = "0",
                    actions = new[] { "READ" },
                    caseIds = new[] { "demo-g-supported" },
                    reason = "Ungültiges Ziel"
                });
            Assert.Equal(HttpStatusCode.NotFound, invalidTarget.StatusCode);

            var invalidScope = await administrator.PostAsJsonAsync(
                "/api/review/administration/entitlement-changes",
                new
                {
                    targetActorId = "synthetic-local:user-alice",
                    expectedEntitlementRevision = "0",
                    actions = new[] { "READ", "UNKNOWN_ACTION" },
                    caseIds = new[] { "demo-g-supported" },
                    reason = "Ungültiger Aktionsumfang"
                });
            Assert.Equal(HttpStatusCode.BadRequest, invalidScope.StatusCode);

            var proposed = await administrator.PostAsJsonAsync(
                "/api/review/administration/entitlement-changes",
                new
                {
                    targetActorId = "synthetic-local:user-alice",
                    expectedEntitlementRevision = "0",
                    actions = new[] { "READ", "ACCEPT" },
                    caseIds = new[] { "demo-g-supported" },
                    reason = "Synthetischer Berechtigungsantrag"
                });
            Assert.Equal(HttpStatusCode.OK, proposed.StatusCode);
            using var proposalJson = JsonDocument.Parse(await proposed.Content.ReadAsStringAsync());
            var proposal = proposalJson.RootElement.GetProperty("proposal");
            changeId = proposal.GetProperty("changeId").GetString()!;
            Assert.Equal("synthetic-local:administrator", proposal.GetProperty("proposerActorId").GetString());
            Assert.Equal("synthetic-local:user-alice", proposal.GetProperty("targetActorId").GetString());
            Assert.DoesNotContain(Administrator, proposalJson.RootElement.GetRawText());

            var forbiddenSelfDecision = await administrator.PostAsJsonAsync(
                "/api/review/administration/entitlement-changes/" + Uri.EscapeDataString(changeId) + "/decision",
                new { approved = true, reason = "Administrator darf nicht selbst entscheiden" });
            Assert.Equal(HttpStatusCode.Forbidden, forbiddenSelfDecision.StatusCode);

            var forbiddenProposal = await approver.PostAsJsonAsync(
                "/api/review/administration/entitlement-changes",
                new
                {
                    targetActorId = "synthetic-local:user-alice",
                    expectedEntitlementRevision = "0",
                    actions = new[] { "READ" },
                    caseIds = new[] { "demo-g-supported" },
                    reason = "Approver darf keinen Antrag stellen"
                });
            Assert.Equal(HttpStatusCode.Forbidden, forbiddenProposal.StatusCode);
        }

        await using (var restarted = Factory(connection))
        {
            using var approver = Client(restarted, Approver);
            using var pending = JsonDocument.Parse(
                await approver.GetStringAsync("/api/review/administration/entitlement-changes/pending?limit=25"));
            var items = pending.RootElement.GetProperty("changes").EnumerateArray().ToArray();
            Assert.Contains(items, item => item.GetProperty("proposal").GetProperty("changeId").GetString() == changeId);

            var decided = await approver.PostAsJsonAsync(
                "/api/review/administration/entitlement-changes/" + Uri.EscapeDataString(changeId) + "/decision",
                new { approved = true, reason = "Synthetische unabhängige Freigabe" });
            Assert.Equal(HttpStatusCode.OK, decided.StatusCode);
            using var decisionJson = JsonDocument.Parse(await decided.Content.ReadAsStringAsync());
            Assert.Equal("synthetic-local:entitlement-approver",
                decisionJson.RootElement.GetProperty("decision").GetProperty("decisionActorId").GetString());

            using var after = JsonDocument.Parse(
                await approver.GetStringAsync("/api/review/administration/entitlement-changes/pending"));
            Assert.DoesNotContain(after.RootElement.GetProperty("changes").EnumerateArray(),
                item => item.GetProperty("proposal").GetProperty("changeId").GetString() == changeId);

            using var administrator = Client(restarted, Administrator);
            using var effective = JsonDocument.Parse(await administrator.GetStringAsync(
                "/api/review/administration/entitlement-changes/effective/"
                + Uri.EscapeDataString("synthetic-local:user-alice")));
            Assert.Equal("1", effective.RootElement.GetProperty("revision").GetString());
            Assert.Equal(new[] { "ACCEPT", "READ" },
                effective.RootElement.GetProperty("actions").EnumerateArray().Select(item => item.GetString()).ToArray());

            // Stored reviewed grants are deliberately not live authorization inputs yet.
            using var alice = Client(restarted, Alice);
            Assert.Equal(HttpStatusCode.OK,
                (await alice.GetAsync("/api/review/work-cases/demo-g-supported")).StatusCode);
        }
    }

    private static HttpClient Client(WebApplicationFactory<Program> host, string token)
    {
        var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static WebApplicationFactory<Program> Factory(string connection)
        => new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("SyntheticReview:Enabled", "true");
            builder.UseSetting("SyntheticReview:PersistenceEnabled", "true");
            builder.UseSetting("ConnectionStrings:SyntheticReview", connection);
            builder.UseSetting("SyntheticReview:Users:alice:Credential", Alice);
            builder.UseSetting("SyntheticReview:Users:alice:Actions:0", "READ");
            builder.UseSetting("SyntheticReview:Users:alice:Actions:1", "ACCEPT");
            builder.UseSetting("SyntheticReview:Users:alice:CaseIds:0", "demo-g-supported");
            builder.UseSetting("SyntheticReview:Administrator:Credential", Administrator);
            builder.UseSetting("SyntheticReview:EntitlementApprover:Credential", Approver);
        });

    private static async Task ResetDatabase(string connection)
    {
        await using var source = NpgsqlDataSource.Create(connection);
        await using var command = source.CreateCommand("DROP SCHEMA IF EXISTS normacase CASCADE");
        await command.ExecuteNonQueryAsync();
    }
}
