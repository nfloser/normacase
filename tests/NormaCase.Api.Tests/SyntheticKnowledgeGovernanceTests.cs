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

public sealed class SyntheticKnowledgeGovernanceTests
{
    private static readonly string Alice = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
    private static readonly string Bob = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
    private static readonly string Other = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
    private const string Path = "/api/review/knowledge/changes";

    [Fact]
    public async Task Exact_assignments_require_distinct_review_and_preserve_server_owned_governance_after_restart()
    {
        var connection = Environment.GetEnvironmentVariable("NORMACASE_POSTGRES_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(connection)) return;
        await using (var source = NpgsqlDataSource.Create(connection))
        await using (var reset = source.CreateCommand("DROP SCHEMA IF EXISTS normacase CASCADE"))
            await reset.ExecuteNonQueryAsync();
        string changeId;
        await using (var host = Factory(connection))
        {
            using var alice = Client(host, Alice);
            using var bob = Client(host, Bob);
            using var other = Client(host, Other);
            using var anonymous = host.CreateClient();
            Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(Path)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await other.GetAsync(Path)).StatusCode);
            using var session = JsonDocument.Parse(await alice.GetStringAsync("/api/review-session"));
            Assert.Equal("PROPOSE", session.RootElement.GetProperty("knowledgeActions")[0].GetString());
            using var packs = JsonDocument.Parse(await alice.GetStringAsync("/api/packs"));
            var pack = packs.RootElement.EnumerateArray().First();
            var request = new { packId = pack.GetProperty("packId").GetString(), releaseId = pack.GetProperty("releaseId").GetString(), sourceReference = "source:synthetic", impactReference = "impact:synthetic", testReference = "tests:synthetic" };
            Assert.Equal(HttpStatusCode.Forbidden, (await bob.PostAsJsonAsync(Path, request)).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await alice.PostAsJsonAsync(Path, new { request.packId, request.releaseId, request.sourceReference, request.impactReference, request.testReference, proposerActorId = "spoofed" })).StatusCode);
            var created = await alice.PostAsJsonAsync(Path, request);
            Assert.Equal(HttpStatusCode.OK, created.StatusCode);
            using var proposal = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
            changeId = proposal.RootElement.GetProperty("changeId").GetString()!;
            Assert.Equal("synthetic-local:user-alice", proposal.RootElement.GetProperty("proposerActorId").GetString());
            Assert.Equal(64, proposal.RootElement.GetProperty("sha256").GetString()!.Length);
            Assert.Equal(HttpStatusCode.Forbidden, (await alice.PostAsJsonAsync(Path + "/" + changeId + "/decision", new { approved = true, reason = "self" })).StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, (await bob.PostAsJsonAsync(Path + "/" + changeId + "/activate", new { expectedRevision = "0" })).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await bob.PostAsJsonAsync(Path + "/" + changeId + "/decision", new { approved = true, reason = "Synthetisch geprüft" })).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await alice.PostAsJsonAsync(Path + "/" + changeId + "/activate", new { expectedRevision = "0" })).StatusCode);
            var activated = await bob.PostAsJsonAsync(Path + "/" + changeId + "/activate", new { expectedRevision = "0" });
            Assert.Equal(HttpStatusCode.OK, activated.StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, (await bob.PostAsJsonAsync(Path + "/" + changeId + "/activate", new { expectedRevision = "0" })).StatusCode);
            using var list = JsonDocument.Parse(await alice.GetStringAsync(Path));
            Assert.Equal(changeId, list.RootElement.GetProperty("changes")[0].GetProperty("changeId").GetString());
            Assert.Equal(HttpStatusCode.BadRequest, (await alice.GetAsync(Path + "?afterChangeId=" + new string('x',257))).StatusCode);
            for (var index = 0; index < 26; index++)
                Assert.Equal(HttpStatusCode.OK, (await alice.PostAsJsonAsync(Path, request)).StatusCode);
            using var firstPage = JsonDocument.Parse(await alice.GetStringAsync(Path));
            Assert.Equal(25, firstPage.RootElement.GetProperty("changes").GetArrayLength());
            var cursor = firstPage.RootElement.GetProperty("nextPageCursor").GetString()!;
            using var secondPage = JsonDocument.Parse(await alice.GetStringAsync(Path + "?afterChangeId=" + Uri.EscapeDataString(cursor)));
            Assert.Equal(2, secondPage.RootElement.GetProperty("changes").GetArrayLength());
            Assert.Equal(JsonValueKind.Null, secondPage.RootElement.GetProperty("nextPageCursor").ValueKind);
            var ids = firstPage.RootElement.GetProperty("changes").EnumerateArray()
                .Concat(secondPage.RootElement.GetProperty("changes").EnumerateArray())
                .Select(item => item.GetProperty("changeId").GetString()).ToArray();
            Assert.Equal(27, ids.Distinct(StringComparer.Ordinal).Count());

        }
        await using var restarted = Factory(connection);
        using var reviewer = Client(restarted, Bob);
        using var retained = JsonDocument.Parse(await reviewer.GetStringAsync(Path + "/" + changeId));
        Assert.Equal("1", retained.RootElement.GetProperty("active").GetProperty("revision").GetString());
        Assert.Equal("synthetic-local:user-bob", retained.RootElement.GetProperty("change").GetProperty("decision").GetProperty("reviewerActorId").GetString());
        using var stillConfigured = JsonDocument.Parse(await reviewer.GetStringAsync("/api/packs"));
        Assert.All(stillConfigured.RootElement.EnumerateArray(), pack => Assert.Equal("SYNTHETIC", pack.GetProperty("validationLevel").GetString()));
    }

    [Fact]
    public void Unknown_and_self_review_assignments_fail_closed_at_startup()
    {
        using var same = Factory("Host=127.0.0.1;Database=unused", "synthetic-local:user-alice");
        Assert.ThrowsAny<Exception>(() => same.CreateClient());
        using var unknown = Factory("Host=127.0.0.1;Database=unused", "synthetic-local:user-unknown");
        Assert.ThrowsAny<Exception>(() => unknown.CreateClient());
    }
    private static WebApplicationFactory<Program> Factory(string connection, string reviewer = "synthetic-local:user-bob")
        => new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("SyntheticReview:Enabled", "true");
            builder.UseSetting("SyntheticReview:PersistenceEnabled", "true");
            builder.UseSetting("ConnectionStrings:SyntheticReview", connection);
            foreach (var (name, credential) in new[] { ("alice", Alice), ("bob", Bob), ("other", Other) })
            {
                builder.UseSetting($"SyntheticReview:Users:{name}:Credential", credential);
                builder.UseSetting($"SyntheticReview:Users:{name}:Actions:0", "READ");
                builder.UseSetting($"SyntheticReview:Users:{name}:CaseIds:0", "demo-g-review");
            }
            builder.UseSetting("SyntheticReview:KnowledgeAdministration:PROPOSE", "synthetic-local:user-alice");
            builder.UseSetting("SyntheticReview:KnowledgeAdministration:REVIEW", reviewer);
            builder.UseSetting("SyntheticReview:KnowledgeAdministration:ACTIVATE", "synthetic-local:user-bob");
        });
    private static HttpClient Client(WebApplicationFactory<Program> host, string credential)
    {
        var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", credential);
        return client;
    }
}
