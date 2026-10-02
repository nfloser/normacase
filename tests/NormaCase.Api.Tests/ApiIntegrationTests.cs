using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using NormaCase.Api;
using NormaCase.Domain.Decision;
using NormaCase.Knowledge.Serialization;
using NormaCase.RuleEngine.Evaluation;
using NormaCase.Serialization;
using Xunit;

namespace NormaCase.Api.Tests;

public sealed class ApiIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    public ApiIntegrationTests(WebApplicationFactory<Program> factory) => _client = factory.CreateClient();

    [Fact]
    public async Task Catalog_contains_only_synthetic_external_packs()
    {
        var response = await _client.GetAsync("/api/packs");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(5, json.RootElement.GetArrayLength());
        foreach (var pack in json.RootElement.EnumerateArray())
            Assert.Equal("SYNTHETIC", pack.GetProperty("validationLevel").GetString());
        Assert.Contains(
            json.RootElement.EnumerateArray(),
            pack => pack.GetProperty("packId").GetString() == "synthetic.demo-e");
        Assert.Equal("no-store", response.Headers.CacheControl!.ToString());
        Assert.Contains("nosniff", response.Headers.GetValues("X-Content-Type-Options"));
    }

    [Theory]
    [InlineData("demo-a-supported", "demo-a", AssessmentOutcome.Supported)]
    [InlineData("demo-a-incomplete", "demo-a", AssessmentOutcome.Incomplete)]
    [InlineData("demo-b-supported", "demo-b", AssessmentOutcome.Supported)]
    [InlineData("demo-c-review", "demo-c", AssessmentOutcome.HumanReview)]
    [InlineData("demo-d-supported", "demo-d", AssessmentOutcome.Supported)]
    [InlineData("demo-e-partial", "demo-e", AssessmentOutcome.Supported)]
    public async Task Http_result_matches_direct_engine_evaluation(string name, string demo, AssessmentOutcome expected)
    {
        var json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Cases", name + ".json"));
        var response = await Post(demo, json);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var document = AssessmentJson.Deserialize(await response.Content.ReadAsStringAsync());
        var input = CaseInputJson.Deserialize(json);
        var pack = new KnowledgePackLoader().LoadFromFile(Path.Combine(AppContext.BaseDirectory, "Knowledge", demo + ".json"));
        var direct = new RuleEvaluator().Evaluate(pack, input.Facts, input.AssessmentDate, input.Evidence);
        Assert.Equal(expected, document.Assessment.Outcome);
        Assert.Equal(AssessmentJson.Serialize(direct, document.PlatformVersion),
            AssessmentJson.Serialize(document.Assessment, document.PlatformVersion));
        Assert.Equal("no-store", response.Headers.CacheControl!.ToString());
    }

    [Fact]
    public async Task Demo_E_http_response_uses_v2_and_preserves_known_and_unknown_outputs()
    {
        var json = File.ReadAllText(
            Path.Combine(
                AppContext.BaseDirectory,
                "Cases",
                "demo-e-partial.json"));

        var response = await Post("demo-e", json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var document = AssessmentJson.Deserialize(
            await response.Content.ReadAsStringAsync());

        Assert.Equal(2, document.FormatVersion);
        Assert.Equal(AssessmentOutcome.Supported, document.Assessment.Outcome);

        var decision = Assert.Single(
            document.Assessment.DomainOutputs,
            output => output.OutputId == "decision_state");
        Assert.Equal(DomainOutputValueKind.Choice, decision.Value.Kind);
        Assert.Equal("ELIGIBLE", decision.Value.Choice);

        var beta = Assert.Single(
            document.Assessment.DomainOutputs,
            output => output.OutputId == "segment_beta");
        Assert.Equal(DomainOutputValueKind.Unknown, beta.Value.Kind);
        Assert.Null(beta.Value.Choice);
        Assert.Equal("SYNTH-DEMO-E-001", beta.Source.Id);
        Assert.Equal("1", beta.Source.Version);
        Assert.Equal(
            "repository:knowledge/demo-e/pack.json",
            beta.Source.SourceLocation);
    }

    [Fact]
    public async Task Invalid_json_has_a_German_error_without_echoing_input()
    {
        var response = await Post("demo-a", "{\"secret_synthetic_marker\":98765}");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("ungültig", JsonDocument.Parse(body).RootElement.GetProperty("message").GetString());
        Assert.DoesNotContain("secret_synthetic_marker", body);
        Assert.DoesNotContain("98765", body);
    }

    [Fact]
    public async Task Unknown_pack_and_wrong_content_type_are_deliberate_errors()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await Post("unknown", "{}")).StatusCode);
        var response = await _client.PostAsync("/api/assessments/synthetic.demo-a", new StringContent("{}"));
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
    }

    [Theory]
    [InlineData("evil.invalid", null)]
    [InlineData("localhost", "https://evil.invalid")]
    [InlineData("localhost", "null")]
    [InlineData("localhost", "http://localhost:1234")]
    public async Task Non_local_hosts_and_origins_are_rejected(string host, string? origin)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/packs");
        request.Headers.Host = host;
        if (origin is not null) request.Headers.Add("Origin", origin);
        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task Same_origin_request_is_allowed()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/packs");
        request.Headers.Add("Origin", "http://localhost");
        Assert.Equal(HttpStatusCode.OK, (await _client.SendAsync(request)).StatusCode);
    }

    [Fact]
    public async Task Oversized_json_is_rejected()
        => Assert.Equal(HttpStatusCode.RequestEntityTooLarge,
            (await Post("demo-a", new string(' ', DemoHost.MaximumBodyBytes + 1))).StatusCode);

    private Task<HttpResponseMessage> Post(string demo, string json)
        => _client.PostAsync("/api/assessments/synthetic." + demo, new StringContent(json, Encoding.UTF8, "application/json"));
}
