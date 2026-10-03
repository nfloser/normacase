using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;
using Xunit;

namespace NormaCase.Api.Tests;

public sealed class SyntheticReviewHostTests
{
    private const string Credential = "AAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh8=";

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
            using var queues = JsonDocument.Parse(await client.GetStringAsync("/api/review/work-queues"));
            Assert.Equal(new[] { 2, 1, 1, 0 },
                queues.RootElement.GetProperty("queues").EnumerateArray()
                    .Select(item => item.GetProperty("items").GetArrayLength()).ToArray());

            using var incomplete = JsonDocument.Parse(await client.GetStringAsync("/api/review/work-cases/demo-g-incomplete"));
            Assert.Empty(incomplete.RootElement.GetProperty("allowedActions").EnumerateArray());
            var denied = await client.PostAsJsonAsync("/api/review/work-cases/demo-g-incomplete/reviews",
                new { expectedCaseRevision = "1", expectedProcessRevision = "1", expectedAuditRevision = "1",
                    disposition = "ACCEPT_SYSTEM_RESULT", reason = "Synthetischer unzulässiger Versuch" });
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);

            var stale = await client.PostAsJsonAsync("/api/review/work-cases/demo-g-supported/reviews",
                new { expectedCaseRevision = "1", expectedProcessRevision = "0", expectedAuditRevision = "1",
                    disposition = "ACCEPT_SYSTEM_RESULT", reason = "Veralteter synthetischer Versuch" });
            Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);

            var accepted = await client.PostAsJsonAsync("/api/review/work-cases/demo-g-supported/reviews",
                new { expectedCaseRevision = "1", expectedProcessRevision = "1", expectedAuditRevision = "1",
                    disposition = "ACCEPT_SYSTEM_RESULT", reason = "Synthetische Freigabe" });
            Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
            using var acceptedJson = JsonDocument.Parse(await accepted.Content.ReadAsStringAsync());
            Assert.Equal("accepted", acceptedJson.RootElement.GetProperty("stateId").GetString());
            Assert.Equal("2", acceptedJson.RootElement.GetProperty("processRevision").GetString());
            var acceptedAudit = acceptedJson.RootElement.GetProperty("audit").EnumerateArray().ToArray();
            Assert.Equal(2, acceptedAudit.Length);
            Assert.Equal("synthetic-local:reviewer", acceptedAudit[1].GetProperty("actorId").GetString());
            Assert.Equal("ACCEPT_SYSTEM_RESULT", acceptedAudit[1].GetProperty("disposition").GetString());

            var repeated = await client.PostAsJsonAsync("/api/review/work-cases/demo-g-supported/reviews",
                new { expectedCaseRevision = "1", expectedProcessRevision = "1", expectedAuditRevision = "1",
                    disposition = "ACCEPT_SYSTEM_RESULT", reason = "Synthetischer Wiederholungsversuch" });
            Assert.Equal(HttpStatusCode.Conflict, repeated.StatusCode);
            var closedState = await client.PostAsJsonAsync("/api/review/work-cases/demo-g-supported/reviews",
                new { expectedCaseRevision = "1", expectedProcessRevision = "2", expectedAuditRevision = "2",
                    disposition = "ACCEPT_SYSTEM_RESULT", reason = "Unzulässiger abgeschlossener Übergang" });
            Assert.Equal(HttpStatusCode.Forbidden, closedState.StatusCode);

            var overridden = await client.PostAsJsonAsync("/api/review/work-cases/demo-g-not-supported/reviews",
                new { expectedCaseRevision = "1", expectedProcessRevision = "1", expectedAuditRevision = "1",
                    disposition = "OVERRIDE", reason = "Synthetische Abweichung", overrideOutcome = "SUPPORTED" });
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

    private static WebApplicationFactory<Program> Factory(string connection)
        => new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("SyntheticReview:Enabled", "true");
            builder.UseSetting("SyntheticReview:PersistenceEnabled", "true");
            builder.UseSetting("SyntheticReview:Credential", Credential);
            builder.UseSetting("ConnectionStrings:SyntheticReview", connection);
        });

    private static async Task ResetDatabase(string connection)
    {
        await using var source = NpgsqlDataSource.Create(connection);
        await using var command = source.CreateCommand("DROP SCHEMA IF EXISTS normacase CASCADE");
        await command.ExecuteNonQueryAsync();
    }
}
