using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NormaCase.Api;
using NormaCase.Application.Reviews;
using NormaCase.Domain.Audit;
using NormaCase.Domain.Cases;
using Xunit;

namespace NormaCase.Api.Tests;

public sealed class ReviewDemoTests
{
    private static readonly string Key = Convert.ToBase64String(Enumerable.Repeat((byte)0xaa, 32).ToArray());
    [Fact]
    public async Task Missing_invalid_and_query_credentials_do_not_authenticate()
    {
        await using var app = Build(); await app.StartAsync(); using var client = app.GetTestClient();
        foreach (var path in new[] { "/api/work-queues", "/api/work-cases/demo-g-supported", "/api/work-queues?key=" + Key })
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(path)).StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Convert.ToBase64String(Enumerable.Repeat((byte)0xbb, 32).ToArray()));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/work-queues")).StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Key);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/work-queues")).StatusCode);
        client.DefaultRequestHeaders.Add("Origin", "https://example.org");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/work-queues")).StatusCode);
    }
    [Fact]
    public async Task Authenticated_accept_preserves_original_and_records_server_actor_then_rejects_stale()
    {
        await using var app = Build(); await app.StartAsync(); using var client = app.GetTestClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Key);
        var before = JsonDocument.Parse(await client.GetStringAsync("/api/work-cases/demo-g-supported")).RootElement;
        var body = Body(before);
        var response = await client.PostAsync("/api/work-cases/demo-g-supported/reviews", new StringContent(body, Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var after = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("reviewed", after.GetProperty("stateId").GetString());
        Assert.Equal(before.GetProperty("assessmentJson").GetString(), after.GetProperty("assessmentJson").GetString());
        Assert.Contains("synthetic-local:reviewer", after.GetProperty("auditJson").GetString());
        Assert.Equal("2", after.GetProperty("auditRevision").GetString());
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync("/api/work-cases/demo-g-supported/reviews", new StringContent(body, Encoding.UTF8, "application/json"))).StatusCode);
    }
    [Fact]
    public async Task Forged_actor_duplicate_fields_and_missing_information_actions_fail_closed()
    {
        await using var app = Build(); await app.StartAsync(); using var client = app.GetTestClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Key);
        var detail = JsonDocument.Parse(await client.GetStringAsync("/api/work-cases/demo-g-incomplete")).RootElement;
        Assert.Equal(0, detail.GetProperty("allowedActions").GetArrayLength());
        var body = Body(detail);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync("/api/work-cases/demo-g-incomplete/reviews", new StringContent(body, Encoding.UTF8, "application/json"))).StatusCode);
        var ready = JsonDocument.Parse(await client.GetStringAsync("/api/work-cases/demo-g-not-supported")).RootElement;
        body = Body(ready);
        foreach (var prefix in new[] { "\"actorId\":\"forged\",", "\"reason\":\"other\"," })
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/work-cases/demo-g-not-supported/reviews", new StringContent("{" + prefix + body[1..], Encoding.UTF8, "application/json"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync("/api/work-cases/demo-technical/reviews", new StringContent(body, Encoding.UTF8, "application/json"))).StatusCode);
    }
    private static string Body(JsonElement detail) => JsonSerializer.Serialize(new
    {
        assessmentId = detail.GetProperty("assessmentId").GetString(),
        caseRevision = detail.GetProperty("caseRevision").GetString(), processRevision = detail.GetProperty("processRevision").GetString(),
        auditRevision = detail.GetProperty("auditRevision").GetString(), disposition = "ACCEPT_SYSTEM_RESULT", reason = "Synthetic HTTP review"
    });
    private static Microsoft.AspNetCore.Builder.WebApplication Build() => DemoHost.Build([], builder =>
    {
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string,string?>
        {
            ["SyntheticReview:Enabled"] = "true", ["SyntheticReview:Credential"] = Key,
            ["NORMACASE_REVIEW_DEMO_CONNECTION"] = "Host=localhost;Database=synthetic-unused"
        });
        builder.Services.AddSingleton<IReviewDemoRepository, ReferenceRepository>();
    });
    private sealed class ReferenceRepository : IReviewDemoRepository
    {
        private readonly Dictionary<CaseId,CaseReviewState> states = [];
        private readonly object gate = new();
        public Task<CaseReviewState> GetAsync(SyntheticCaseSeed seed, SyntheticWorkload workload, CancellationToken token)
        {
            lock(gate)
            {
                if (!states.TryGetValue(seed.Process.CaseId, out var state))
                {
                    var record = seed.Assessment!;
                    state = new(record, seed.Process.CaseRevision, seed.Process, AssessmentAuditTrail.Start(
                        AssessmentAuditEvent.AssessmentCreated(1, record.AssessmentId, record.RecordedAtUtc, "synthetic-intake")));
                    states.Add(seed.Process.CaseId, state);
                }
                return Task.FromResult(state);
            }
        }
        public Task<CaseReviewState> ExecuteAsync(CaseId caseId, Func<CaseReviewState,CaseReviewState> update, CancellationToken cancellationToken = default)
        {
            lock(gate) { cancellationToken.ThrowIfCancellationRequested(); var next = update(states[caseId]); states[caseId] = next; return Task.FromResult(next); }
        }
    }
}
