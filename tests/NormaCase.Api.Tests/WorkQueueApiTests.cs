using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace NormaCase.Api.Tests;

public sealed class WorkQueueApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient client;

    public WorkQueueApiTests(WebApplicationFactory<Program> factory)
        => client = factory.CreateClient();

    [Fact]
    public async Task Synthetic_queue_catalog_contains_the_four_critical_path_queues()
    {
        var response = await client.GetAsync("/api/work-queues");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl!.ToString());

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = json.RootElement;
        Assert.Equal("synthetic-demo-work-queues", root.GetProperty("configurationId").GetString());
        Assert.Equal(1, root.GetProperty("configurationVersion").GetInt32());

        var queues = root.GetProperty("queues").EnumerateArray().ToArray();
        Assert.Equal(
            ["approval", "clarification", "review", "technical"],
            queues.Select(queue => queue.GetProperty("queueId").GetString()).ToArray());

        foreach (var queue in queues)
            Assert.Single(queue.GetProperty("items").EnumerateArray());

        Assert.Contains(
            queues,
            queue => queue.GetProperty("queueId").GetString() == "technical"
                && Assert.Single(queue.GetProperty("items").EnumerateArray())
                    .GetProperty("hasAssessment").GetBoolean() == false);
    }

    [Fact]
    public async Task Assessed_work_item_exposes_recorded_trace_evidence_and_revisions()
    {
        var response = await client.GetAsync("/api/work-items/synthetic-approval-001");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = json.RootElement;

        Assert.Equal("synthetic-approval-001", root.GetProperty("caseId").GetString());
        Assert.Equal(1, root.GetProperty("caseRevision").GetInt64());
        Assert.Equal("awaiting-approval", root.GetProperty("stateId").GetString());
        Assert.Equal(1, root.GetProperty("processRevision").GetInt64());
        Assert.Equal("approval", root.GetProperty("queueId").GetString());
        Assert.Equal("RECORDED", root.GetProperty("assessmentStatus").GetString());

        var assessment = root.GetProperty("assessment");
        Assert.Equal("SUPPORTED", assessment.GetProperty("outcome").GetString());
        Assert.Equal("READY_FOR_APPROVAL", assessment.GetProperty("routingDisposition").GetString());
        Assert.Equal("synthetic.demo-g", assessment.GetProperty("knowledgePackId").GetString());
        Assert.Equal("demo-g-2026.1", assessment.GetProperty("knowledgeRelease").GetString());
        Assert.Equal("2026-10-03", assessment.GetProperty("assessmentDate").GetString());

        var evidence = Assert.Single(assessment.GetProperty("evidence").EnumerateArray());
        Assert.Equal("supporting_document", evidence.GetProperty("id").GetString());
        Assert.Equal("PRESENT", evidence.GetProperty("status").GetString());

        using var assessmentJson = JsonDocument.Parse(
            assessment.GetProperty("assessmentJson").GetString()!);
        var trace = assessmentJson.RootElement
            .GetProperty("assessment")
            .GetProperty("ruleTrace");
        Assert.Equal("DEMO-G-DECISION", trace.GetProperty("ruleId").GetString());
        Assert.Equal(
            "SYNTH-DEMO-G-001",
            trace.GetProperty("source").GetProperty("id").GetString());
    }

    [Fact]
    public async Task Technical_work_item_never_fabricates_an_assessment()
    {
        var response = await client.GetAsync("/api/work-items/synthetic-technical-001");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = json.RootElement;

        Assert.Equal("technical", root.GetProperty("queueId").GetString());
        Assert.Equal("integration-error", root.GetProperty("stateId").GetString());
        Assert.Equal("NOT_RECORDED", root.GetProperty("assessmentStatus").GetString());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("assessment").ValueKind);
    }

    [Fact]
    public async Task Unknown_work_item_returns_not_found_without_echoing_the_id()
    {
        var response = await client.GetAsync("/api/work-items/secret-case-marker");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("secret-case-marker", body);
    }
}
