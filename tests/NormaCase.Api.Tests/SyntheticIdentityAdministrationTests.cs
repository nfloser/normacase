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

public sealed class SyntheticIdentityAdministrationTests
{
    private static readonly string Alice = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
    private static readonly string Bob = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
    private static readonly string Administrator = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
    private static readonly string EntitlementApprover = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    [Fact]
    public async Task Administrator_suspends_and_reactivates_a_user_live_with_persistent_audit()
    {
        var connection = Environment.GetEnvironmentVariable("NORMACASE_POSTGRES_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(connection)) return;
        await ResetDatabase(connection);
        const string actor = "synthetic-local:user-alice";

        await using (var host = Factory(connection))
        {
            using var alice = Client(host, Alice);
            Assert.Equal(HttpStatusCode.OK, (await alice.GetAsync("/api/review-session")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden,
                (await alice.GetAsync("/api/review/administration/identities")).StatusCode);

            using var administrator = Client(host, Administrator);
            var selfTarget = await administrator.PostAsJsonAsync(
                "/api/review/administration/identities/synthetic-local%3Aadministrator/status",
                new { expectedRevision = "0", suspended = true, reason = "Unzulässige Selbstsperre" });
            Assert.Equal(HttpStatusCode.NotFound, selfTarget.StatusCode);
            using var initial = JsonDocument.Parse(
                await administrator.GetStringAsync("/api/review/administration/identities"));
            var aliceState = initial.RootElement.GetProperty("identities").EnumerateArray()
                .Single(item => item.GetProperty("actorId").GetString() == actor);
            Assert.Equal("0", aliceState.GetProperty("revision").GetString());
            Assert.False(aliceState.GetProperty("suspended").GetBoolean());

            var suspended = await administrator.PostAsJsonAsync(
                "/api/review/administration/identities/" + Uri.EscapeDataString(actor) + "/status",
                new { expectedRevision = "0", suspended = true, reason = "Synthetische Live-Sperre" });
            Assert.Equal(HttpStatusCode.OK, suspended.StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await alice.GetAsync("/api/review-session")).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await alice.GetAsync("/api/review/work-queues")).StatusCode);

            var stale = await administrator.PostAsJsonAsync(
                "/api/review/administration/identities/" + Uri.EscapeDataString(actor) + "/status",
                new { expectedRevision = "0", suspended = false, reason = "Veraltete Reaktivierung" });
            Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
            using var detail = JsonDocument.Parse(await administrator.GetStringAsync(
                "/api/review/administration/identities/" + Uri.EscapeDataString(actor)));
            var audit = detail.RootElement.GetProperty("history").EnumerateArray().Single();
            Assert.Equal("synthetic-local:administrator", audit.GetProperty("administratorActorId").GetString());
            Assert.Equal("Synthetische Live-Sperre", audit.GetProperty("reason").GetString());
        }

        await using var restarted = Factory(connection);
        using var stillSuspended = Client(restarted, Alice);
        Assert.Equal(HttpStatusCode.Unauthorized, (await stillSuspended.GetAsync("/api/review-session")).StatusCode);
        using var adminAfterRestart = Client(restarted, Administrator);
        var active = await adminAfterRestart.PostAsJsonAsync(
            "/api/review/administration/identities/" + Uri.EscapeDataString(actor) + "/status",
            new { expectedRevision = "1", suspended = false, reason = "Synthetische Reaktivierung" });
        Assert.Equal(HttpStatusCode.OK, active.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await stillSuspended.GetAsync("/api/review-session")).StatusCode);

        var changeId = "entitlement-" + Guid.NewGuid().ToString("N");
        var proposed = await adminAfterRestart.PostAsJsonAsync(
            "/api/review/administration/entitlement-changes/", new
            {
                changeId,
                targetActorId = actor,
                expectedEntitlementRevision = "0",
                actions = new[] { "READ" },
                caseIds = new[] { "demo-g-supported" },
                reason = "Synthetischer Berechtigungsantrag"
            });
        Assert.Equal(HttpStatusCode.Created, proposed.StatusCode);
        using (var proposalJson = JsonDocument.Parse(await proposed.Content.ReadAsStringAsync()))
        {
            Assert.Equal("synthetic-local:administrator",
                proposalJson.RootElement.GetProperty("proposerActorId").GetString());
            Assert.True(DateTimeOffset.TryParse(proposalJson.RootElement.GetProperty("proposedAtUtc").GetString(), out _));
        }
        Assert.Equal(HttpStatusCode.Forbidden,
            (await adminAfterRestart.GetAsync("/api/review/administration/entitlement-changes/pending")).StatusCode);

        using var approver = Client(restarted, EntitlementApprover);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await approver.GetAsync("/api/review/administration/identities")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await approver.PostAsJsonAsync(
            "/api/review/administration/entitlement-changes/", new
            {
                changeId = "entitlement-" + Guid.NewGuid().ToString("N"), targetActorId = actor,
                expectedEntitlementRevision = "0", actions = new[] { "READ" },
                caseIds = new[] { "demo-g-supported" }, reason = "Unzulässiger Antrag"
            })).StatusCode);
        using var pending = JsonDocument.Parse(await approver.GetStringAsync(
            "/api/review/administration/entitlement-changes/pending"));
        Assert.Equal(changeId, pending.RootElement.GetProperty("changes").EnumerateArray().Single()
            .GetProperty("changeId").GetString());
        var decided = await approver.PostAsJsonAsync(
            "/api/review/administration/entitlement-changes/" + changeId + "/decision",
            new { approved = true, reason = "Synthetische Gegenprüfung" });
        Assert.Equal(HttpStatusCode.OK, decided.StatusCode);
        using (var context = JsonDocument.Parse(await adminAfterRestart.GetStringAsync(
            "/api/review/administration/entitlement-changes/context")))
            Assert.True(context.RootElement.GetProperty("authorizationActive").GetBoolean());
        using (var effective = JsonDocument.Parse(await stillSuspended.GetStringAsync(
            "/api/review/work-cases/demo-g-supported")))
            Assert.Empty(effective.RootElement.GetProperty("allowedActions").EnumerateArray());
        await using var verificationSource = NpgsqlDataSource.Create(connection);
        Assert.Empty(await new NormaCase.Persistence.PostgreSql.PostgresReviewedEntitlementChangeStore(
            verificationSource).ListPendingAsync(100));
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
            builder.UseSetting("SyntheticReview:Users:bob:Credential", Bob);
            builder.UseSetting("SyntheticReview:Users:bob:Actions:0", "READ");
            builder.UseSetting("SyntheticReview:Users:bob:CaseIds:0", "demo-g-not-supported");
            builder.UseSetting("SyntheticReview:Administrator:Credential", Administrator);
            builder.UseSetting("SyntheticReview:EntitlementApprover:Credential", EntitlementApprover);
        });

    private static async Task ResetDatabase(string connection)
    {
        await using var source = NpgsqlDataSource.Create(connection);
        await using var command = source.CreateCommand("DROP SCHEMA IF EXISTS normacase CASCADE");
        await command.ExecuteNonQueryAsync();
    }
}
