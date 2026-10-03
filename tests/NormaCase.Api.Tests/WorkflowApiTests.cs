using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc.Testing;
using NormaCase.Api;
using NormaCase.Application.Workflows;
using NormaCase.Domain.Cases;
using NormaCase.Domain.Workflow;
using NormaCase.Serialization;
using Xunit;

namespace NormaCase.Api.Tests;

public sealed class WorkflowApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    public WorkflowApiTests(WebApplicationFactory<Program> factory) => _client = factory.CreateClient();

    [Fact]
    public async Task Actual_workflow_can_start_return_complete_and_resume_from_export()
    {
        var json = await Start();
        var initial = WorkflowRunRecordJson.Deserialize(json);
        Assert.Equal(new CaseId("case-synthetic-api"), initial.CaseId);
        var response = await Advance(json, 0, "submit");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var review = await RunJson(response);
        Assert.Equal("review", WorkflowRunRecordJson.Deserialize(review).Current.StateId);
        var imported = await Post("/api/workflows/verify", review);
        Assert.Equal(HttpStatusCode.OK, imported.StatusCode);
        Assert.Equal(review, await RunJson(imported));
        var returned = await Advance(review, 1, "return");
        var draft = await RunJson(returned);
        var submitted = await Advance(draft, 2, "submit");
        var completed = await Advance(await RunJson(submitted), 3, "finish");
        var completedText = await completed.Content.ReadAsStringAsync();
        using var envelope = JsonDocument.Parse(completedText);
        Assert.Equal("Abgeschlossen", envelope.RootElement.GetProperty("view").GetProperty("stateLabel").GetString());
        Assert.True(envelope.RootElement.GetProperty("view").GetProperty("terminal").GetBoolean());
        Assert.Equal(0, envelope.RootElement.GetProperty("view").GetProperty("transitions").GetArrayLength());
        Assert.Equal(5, envelope.RootElement.GetProperty("view").GetProperty("history").GetArrayLength());
        Assert.Equal("no-store", completed.Headers.CacheControl!.ToString());
        Assert.Equal("draft", initial.Current.StateId);
    }

    [Fact]
    public async Task Stale_revision_unavailable_transition_and_backward_time_are_private_errors()
    {
        var initial = await Start();
        var conflict = await Advance(initial, 99, "submit");
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        Assert.Contains("Revision", await conflict.Content.ReadAsStringAsync());
        var invalidTransition = await Advance(initial, 0, "finish");
        Assert.Equal(HttpStatusCode.Conflict, invalidTransition.StatusCode);
        var backwards = await Advance(initial, 0, "submit", "2020-01-01T00:00:00Z");
        Assert.Equal(HttpStatusCode.BadRequest, backwards.StatusCode);
        Assert.DoesNotContain("private-reason", await backwards.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Internally_valid_but_substituted_release_graph_source_or_platform_is_rejected()
    {
        var json = await Start();
        Action<JsonNode>[] mutations =
        [
            node => node["run"]!["platformVersion"] = "private-platform",
            node => node["run"]!["history"]![0]!["snapshot"]!["knowledgeRelease"] = "private-release",
            node => node["run"]!["history"]![0]!["snapshot"]!["source"]!["title"] = "private-source-title",
            node => node["run"]!["history"]![0]!["snapshot"]!["transitions"]![0]!["toStateId"] = "done"
        ];
        foreach (var mutate in mutations)
        {
            var node = JsonNode.Parse(json)!;
            mutate(node);
            var altered = node.ToJsonString();
            _ = WorkflowRunRecordJson.Deserialize(altered); // internally consistent creation is not provenance
            var response = await Post("/api/workflows/verify", altered);
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            Assert.DoesNotContain("private-", await response.Content.ReadAsStringAsync());
            response = await Advance(altered, 0, "submit");
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        }
    }

    [Theory]
    [InlineData("/api/workflows/synthetic.demo-f/start")]
    [InlineData("/api/workflows/advance")]
    [InlineData("/api/workflows/verify")]
    public async Task Routes_keep_JSON_loopback_origin_and_size_guards(string path)
    {
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, (await _client.PostAsync(path, new StringContent("{}"))).StatusCode);
        using var request = new HttpRequestMessage(HttpMethod.Post, path);
        request.Headers.Add("Origin", "https://example.invalid");
        request.Content = new StringContent("{}", Encoding.UTF8, "application/json");
        Assert.Equal(HttpStatusCode.Forbidden, (await _client.SendAsync(request)).StatusCode);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, (await Post(path, new string(' ', DemoHost.MaximumBodyBytes + 1))).StatusCode);
    }

    [Fact]
    public async Task Duplicate_unknown_and_missing_commands_do_not_generate_metadata()
    {
        var command = StartCommand();
        command.Remove("recordedAtUtc");
        Assert.Equal(HttpStatusCode.BadRequest, (await Post("/api/workflows/synthetic.demo-f/start", command.ToJsonString())).StatusCode);
        command = StartCommand();
        command["extra"] = true;
        Assert.Equal(HttpStatusCode.BadRequest, (await Post("/api/workflows/synthetic.demo-f/start", command.ToJsonString())).StatusCode);
        var duplicate = StartCommand().ToJsonString().Replace("\"runId\":", "\"runId\":\"duplicate-private\",\"runId\":");
        Assert.Equal(HttpStatusCode.BadRequest, (await Post("/api/workflows/synthetic.demo-f/start", duplicate)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Post("/api/workflows/unknown/start", "{}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Post("/api/workflows/verify", "{}")).StatusCode);
    }

    private async Task<string> Start()
    {
        var response = await Post("/api/workflows/synthetic.demo-f/start", StartCommand().ToJsonString());
        response.EnsureSuccessStatusCode();
        return await RunJson(response);
    }

    private static JsonObject StartCommand() => new()
    {
        ["workflowId"] = "synthetic.review", ["workflowVersion"] = 1, ["runId"] = "run-synthetic-api",
        ["caseId"] = "case-synthetic-api", ["actorId"] = "synthetic-actor",
        ["recordedAtUtc"] = "2026-10-03T12:00:00Z", ["reason"] = "Synthetischer Start"
    };

    private Task<HttpResponseMessage> Advance(string runJson, long revision, string transition, string time = "2026-10-03T12:01:00Z")
        => Post("/api/workflows/advance", JsonSerializer.Serialize(new
        {
            runJson, expectedRevision = revision, transitionId = transition, actorId = "synthetic-reviewer",
            recordedAtUtc = time, reason = "private-reason"
        }));

    private static async Task<string> RunJson(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("runJson").GetString()!;
    }

    private Task<HttpResponseMessage> Post(string path, string json)
        => _client.PostAsync(path, new StringContent(json, Encoding.UTF8, "application/json"));
}
