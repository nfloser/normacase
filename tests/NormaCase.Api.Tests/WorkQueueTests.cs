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
    public async Task Queues_are_repeatable_and_summarize_exactly_one_hundred_routed_cases()
    {
        var response = await client.GetAsync("/api/work-queues");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl!.ToString());
        var text = await response.Content.ReadAsStringAsync();
        Assert.Equal(text, await client.GetStringAsync("/api/work-queues"));

        using var document = JsonDocument.Parse(text);
        var root = document.RootElement;
        Assert.Equal(100, root.GetProperty("totalCases").GetInt32());
        Assert.Equal(5, root.GetProperty("previewLimit").GetInt32());

        var queues = root.GetProperty("queues").EnumerateArray().ToArray();
        Assert.Equal(4, queues.Length);
        var counts = queues.ToDictionary(
            queue => queue.GetProperty("queueId").GetString()!,
            queue => queue.GetProperty("totalCount").GetInt32(),
            StringComparer.Ordinal);
        Assert.Equal(40, counts["approval"]);
        Assert.Equal(20, counts["clarification"]);
        Assert.Equal(20, counts["review"]);
        Assert.Equal(20, counts["technical"]);
        Assert.Equal(100, counts.Values.Sum());

        Assert.All(queues, queue => Assert.Equal(5, queue.GetProperty("items").GetArrayLength()));
        Assert.Equal(20, queues.Sum(queue => queue.GetProperty("items").GetArrayLength()));
        foreach (var item in queues.SelectMany(queue => queue.GetProperty("items").EnumerateArray()))
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
    [InlineData("demo-g-supported-02", AssessmentOutcome.Supported)]
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

    [Theory]
    [InlineData("demo-technical")]
    [InlineData("demo-technical-02")]
    public async Task Technical_cases_have_no_fabricated_assessment(string id)
    {
        using var document = JsonDocument.Parse(await client.GetStringAsync("/api/work-cases/" + id));
        var root = document.RootElement;
        Assert.Equal("technical", root.GetProperty("queueId").GetString());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("assessmentJson").ValueKind);
        Assert.Equal(JsonValueKind.Null, root.GetProperty("assessmentId").ValueKind);
        Assert.Empty(root.GetProperty("evidence").EnumerateObject());
    }

    [Fact]
    public async Task Unknown_case_is_not_found_and_cross_origin_queue_reads_are_denied()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/work-cases/unknown")).StatusCode);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/work-queues");
        request.Headers.Add("Origin", "https://example.org");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(request)).StatusCode);
    }
}
