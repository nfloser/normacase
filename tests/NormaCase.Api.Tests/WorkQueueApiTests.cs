using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using NormaCase.Api;
using NormaCase.Domain.Decision;
using NormaCase.Serialization;
using Xunit;

namespace NormaCase.Api.Tests;

public sealed class WorkQueueApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient client;

    public WorkQueueApiTests(WebApplicationFactory<Program> factory)
        => client = factory.CreateClient();

    [Fact]
    public async Task Synthetic_workload_exposes_four_state_driven_queues()
    {
        var response = await client.GetAsync("/api/work-queues");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl!.ToString());

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;
        Assert.Equal("synthetic.work-queues", root.GetProperty("configurationId").GetString());
        Assert.Equal(1, root.GetProperty("configurationVersion").GetInt32());

        var queues = root.GetProperty("queues").EnumerateArray()
            .ToDictionary(
                queue => queue.GetProperty("id").GetString()!,
                StringComparer.Ordinal);

        Assert.Equal(
            new[] { "approval", "clarification", "review", "technical" },
            queues.Keys.Order(StringComparer.Ordinal).ToArray());

        foreach (var queue in queues.Values)
            Assert.Single(queue.GetProperty("items").EnumerateArray());

        var approval = queues["approval"].GetProperty("items")[0];
        Assert.Equal("synthetic-case-approval", approval.GetProperty("caseId").GetString());
        Assert.True(approval.GetProperty("hasAssessment").GetBoolean());
        Assert.Equal("SUPPORTED", approval.GetProperty("assessmentOutcome").GetString());
        Assert.Equal("READY_FOR_APPROVAL", approval.GetProperty("routingDisposition").GetString());

        var technical = queues["technical"].GetProperty("items")[0];
        Assert.Equal("synthetic-case-technical", technical.GetProperty("caseId").GetString());
        Assert.False(technical.GetProperty("hasAssessment").GetBoolean());
        Assert.Equal(JsonValueKind.Null, technical.GetProperty("assessmentOutcome").ValueKind);
        Assert.Equal(JsonValueKind.Null, technical.GetProperty("routingDisposition").ValueKind);
    }

    [Fact]
    public async Task Assessed_work_item_exposes_exact_recorded_assessment_for_drill_down()
    {
        var response = await client.GetAsync(
            "/api/work-queues/cases/synthetic-case-approval");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;
        Assert.Equal("approval", root.GetProperty("queueId").GetString());
        Assert.Equal("awaiting-approval", root.GetProperty("stateId").GetString());
        Assert.Equal(1, root.GetProperty("processRevision").GetInt64());
        Assert.True(root.GetProperty("hasAssessment").GetBoolean());
        Assert.Equal("synthetic.demo-c", root.GetProperty("knowledgePackId").GetString());
        Assert.Equal(
            "PRESENT",
            root.GetProperty("evidence").GetProperty("verification").GetString());
        Assert.Equal(
            "READY_FOR_APPROVAL",
            root.GetProperty("routing").GetProperty("disposition").GetString());

        var assessmentJson = root.GetProperty("assessmentJson").GetString();
        Assert.False(string.IsNullOrWhiteSpace(assessmentJson));
        var assessment = AssessmentJson.Deserialize(assessmentJson!);
        Assert.Equal(AssessmentOutcome.Supported, assessment.Assessment.Outcome);
        Assert.NotNull(assessment.Assessment.RuleTrace);
        Assert.Equal("synthetic.demo-c", assessment.Assessment.KnowledgePackId);
    }

    [Fact]
    public async Task Technical_work_item_does_not_fabricate_an_assessment()
    {
        var response = await client.GetAsync(
            "/api/work-queues/cases/synthetic-case-technical");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;
        Assert.Equal("technical", root.GetProperty("queueId").GetString());
        Assert.Equal("integration-error", root.GetProperty("stateId").GetString());
        Assert.False(root.GetProperty("hasAssessment").GetBoolean());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("assessmentId").ValueKind);
        Assert.Equal(JsonValueKind.Null, root.GetProperty("assessmentOutcome").ValueKind);
        Assert.Equal(JsonValueKind.Null, root.GetProperty("assessmentJson").ValueKind);
        Assert.Equal(JsonValueKind.Null, root.GetProperty("evidence").ValueKind);
        Assert.Equal(JsonValueKind.Null, root.GetProperty("routing").ValueKind);
        Assert.Equal(
            "synthetic-adapter-unavailable",
            root.GetProperty("technicalReasonCode").GetString());
    }

    [Fact]
    public async Task Unknown_work_item_returns_a_German_bounded_error()
    {
        var response = await client.GetAsync("/api/work-queues/cases/not-installed");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);
        Assert.Equal("unknown_work_item", document.RootElement.GetProperty("code").GetString());
        Assert.Contains(
            "nicht gefunden",
            document.RootElement.GetProperty("message").GetString(),
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Exception", body, StringComparison.Ordinal);
    }
}
