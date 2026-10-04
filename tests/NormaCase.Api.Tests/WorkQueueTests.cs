using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using NormaCase.Serialization;
using NormaCase.Domain.Decision;
using Xunit;

namespace NormaCase.Api.Tests;

public sealed class WorkQueueTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient client;
    public WorkQueueTests(WebApplicationFactory<Program> factory) => client = factory.CreateClient();

    [Fact]
    public async Task Queues_are_repeatable_and_include_positive_and_negative_approval_candidates()
    {
        var response = await client.GetAsync("/api/work-queues");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl!.ToString());
        var text = await response.Content.ReadAsStringAsync();
        Assert.Equal(text, await client.GetStringAsync("/api/work-queues"));
        using var document = JsonDocument.Parse(text);
        Assert.Equal(100, document.RootElement.GetProperty("totalCases").GetInt32());
        var queues = document.RootElement.GetProperty("queues").EnumerateArray().ToArray();
        Assert.Equal(4, queues.Length);
        Assert.Equal(new[] { 60, 20, 15, 5 }, queues.Select(q => q.GetProperty("items").GetArrayLength()));
        var items = queues.SelectMany(q => q.GetProperty("items").EnumerateArray()).ToArray();
        Assert.Equal(100, items.Length);
        Assert.Equal(100, items.Select(item => item.GetProperty("caseId").GetString()).Distinct(StringComparer.Ordinal).Count());
        foreach (var item in items)
        {
            Assert.Equal("1", item.GetProperty("caseRevision").GetString());
            Assert.Equal("1", item.GetProperty("processRevision").GetString());
        }
    }

    [Theory]
    [InlineData("demo-g-supported", AssessmentOutcome.Supported)]
    [InlineData("demo-g-not-supported", AssessmentOutcome.NotSupported)]
    [InlineData("demo-g-incomplete", AssessmentOutcome.Incomplete)]
    [InlineData("demo-g-review", AssessmentOutcome.HumanReview)]
    public async Task Detail_retains_assessment_trace_and_evidence(string id, AssessmentOutcome expected)
    {
        using var document = JsonDocument.Parse(await client.GetStringAsync("/api/work-cases/" + id));
        var root = document.RootElement;
        var assessment = AssessmentJson.Deserialize(root.GetProperty("assessmentJson").GetString()!);
        Assert.Equal(expected, assessment.Assessment.Outcome);
        Assert.Equal("synthetic.demo-g", root.GetProperty("packId").GetString());
        Assert.NotEqual(JsonValueKind.Null, root.GetProperty("assessmentId").ValueKind);
        Assert.Single(root.GetProperty("evidence").EnumerateObject());
        Assert.True(assessment.Assessment.RuleTrace is not null || expected == AssessmentOutcome.Incomplete);
    }

    [Fact]
    public async Task Technical_case_has_no_fabricated_assessment_and_unknown_case_is_not_found()
    {
        using var document = JsonDocument.Parse(await client.GetStringAsync("/api/work-cases/demo-technical"));
        var root = document.RootElement;
        Assert.Equal("technical", root.GetProperty("queueId").GetString());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("assessmentJson").ValueKind);
        Assert.Equal(JsonValueKind.Null, root.GetProperty("assessmentId").ValueKind);
        Assert.Empty(root.GetProperty("evidence").EnumerateObject());
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/work-cases/unknown")).StatusCode);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/work-queues");
        request.Headers.Add("Origin", "https://example.org");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(request)).StatusCode);
    }
}
