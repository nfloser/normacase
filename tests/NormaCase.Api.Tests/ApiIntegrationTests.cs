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
    public async Task Operational_health_is_local_bounded_and_ready_without_persistence()
    {
        var live = await _client.GetAsync("/health/live");
        var ready = await _client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
        Assert.Equal(HttpStatusCode.OK, ready.StatusCode);
        using var liveJson = JsonDocument.Parse(await live.Content.ReadAsStringAsync());
        using var readyJson = JsonDocument.Parse(await ready.Content.ReadAsStringAsync());
        Assert.Equal("verfügbar", liveJson.RootElement.GetProperty("status").GetString());
        Assert.Equal("bereit", readyJson.RootElement.GetProperty("status").GetString());
        Assert.Equal("nicht aktiviert", readyJson.RootElement.GetProperty("persistence").GetString());
        Assert.Equal(2, liveJson.RootElement.EnumerateObject().Count());
        Assert.Equal(2, readyJson.RootElement.EnumerateObject().Count());
        Assert.Equal("no-store", live.Headers.CacheControl!.ToString());
        Assert.Equal("no-store", ready.Headers.CacheControl!.ToString());

        using var nonLocal = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        nonLocal.Headers.Host = "example.invalid";
        Assert.Equal(HttpStatusCode.Forbidden, (await _client.SendAsync(nonLocal)).StatusCode);
    }

    [Fact]
    public async Task Catalog_contains_only_synthetic_external_packs()
    {
        var response = await _client.GetAsync("/api/packs");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(7, json.RootElement.GetArrayLength());
        foreach (var pack in json.RootElement.EnumerateArray())
        {
            Assert.Equal("SYNTHETIC", pack.GetProperty("validationLevel").GetString());
            Assert.Equal("de-DE", pack.GetProperty("presentation").GetProperty("locale").GetString());
            Assert.False(string.IsNullOrWhiteSpace(pack.GetProperty("presentation").GetProperty("name").GetString()));
        }
        Assert.Equal("no-store", response.Headers.CacheControl!.ToString());
        Assert.Contains("nosniff", response.Headers.GetValues("X-Content-Type-Options"));
        Assert.Contains("no-referrer", response.Headers.GetValues("Referrer-Policy"));
        Assert.Contains("DENY", response.Headers.GetValues("X-Frame-Options"));
        Assert.Contains(
            "frame-ancestors 'none'",
            response.Headers.GetValues("Content-Security-Policy").Single());
    }

    [Theory]
    [InlineData("demo-a-supported", "demo-a", AssessmentOutcome.Supported)]
    [InlineData("demo-a-incomplete", "demo-a", AssessmentOutcome.Incomplete)]
    [InlineData("demo-b-supported", "demo-b", AssessmentOutcome.Supported)]
    [InlineData("demo-c-review", "demo-c", AssessmentOutcome.HumanReview)]
    [InlineData("demo-d-supported", "demo-d", AssessmentOutcome.Supported)]
    [InlineData("demo-e-partial", "demo-e", AssessmentOutcome.Supported)]
    [InlineData("demo-g-incomplete", "demo-g", AssessmentOutcome.Incomplete)]
    [InlineData("demo-g-review", "demo-g", AssessmentOutcome.HumanReview)]
    [InlineData("demo-g-supported", "demo-g", AssessmentOutcome.Supported)]
    [InlineData("demo-g-not-supported", "demo-g", AssessmentOutcome.NotSupported)]
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
    public async Task Pitch_scenario_manifest_matches_case_results_and_trace_identity()
    {
        var scenarioJson = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "Scenarios", "pitch-demo-v1.json"));
        using var scenario = JsonDocument.Parse(scenarioJson);
        var root = scenario.RootElement;

        Assert.Equal("SYNTHETIC", root.GetProperty("validationLevel").GetString());
        Assert.Equal("synthetic.demo-g", root.GetProperty("packId").GetString());
        var releaseId = root.GetProperty("releaseId").GetString();
        var ruleId = root.GetProperty("ruleId").GetString();
        var sourceId = root.GetProperty("sourceId").GetString();

        foreach (var step in root.GetProperty("steps").EnumerateArray())
        {
            var caseJson = File.ReadAllText(
                Path.Combine(AppContext.BaseDirectory, "Cases", step.GetProperty("caseFile").GetString()!));
            var response = await Post("demo-g", caseJson);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            using var actual = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var assessment = actual.RootElement.GetProperty("assessment");
            Assert.Equal(step.GetProperty("expectedOutcome").GetString(),
                assessment.GetProperty("outcome").GetString());
            Assert.Equal(releaseId, assessment.GetProperty("knowledgeRelease").GetString());

            var trace = assessment.GetProperty("ruleTrace");
            Assert.Equal(ruleId, trace.GetProperty("ruleId").GetString());
            Assert.Equal(sourceId, trace.GetProperty("sourceId").GetString());

            var expectedMissing = step.GetProperty("expectedMissingRequiredFields")
                .EnumerateArray()
                .Select(item => item.GetString())
                .ToArray();
            var actualMissing = assessment.GetProperty("missingRequiredFields")
                .EnumerateArray()
                .Select(item => item.GetString())
                .ToArray();
            Assert.Equal(expectedMissing, actualMissing);
        }
    }

    [Fact]
    public async Task Catalog_exposes_external_German_labels_and_examples_for_Demo_E()
    {
        var response = await _client.GetAsync("/api/packs");
        response.EnsureSuccessStatusCode();

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var demo = Assert.Single(
            json.RootElement.EnumerateArray(),
            item => item.GetProperty("packId").GetString() == "synthetic.demo-e");

        var presentation = demo.GetProperty("presentation");
        Assert.Equal("Demo E – Mehrere Ergebnisse", presentation.GetProperty("name").GetString());

        var metric = Assert.Single(
            demo.GetProperty("fields").EnumerateArray(),
            item => item.GetProperty("id").GetString() == "metric");
        Assert.Equal("Kennzahl", metric.GetProperty("label").GetString());

        Assert.Contains(
            presentation.GetProperty("examples").EnumerateArray(),
            item => item.GetProperty("id").GetString() == "mixed"
                && item.GetProperty("label").GetString() == "Bekannte und unbekannte Ausgaben");
    }

    [Fact]
    public async Task Synthetic_example_can_be_loaded_without_a_file_or_technical_case_id()
    {
        var response = await _client.GetAsync("/api/packs/synthetic.demo-e/examples/mixed");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("2026-10-02", document.RootElement.GetProperty("assessmentDate").GetString());
        Assert.Equal("YES", document.RootElement.GetProperty("values").GetProperty("gate_primary").GetString());
        Assert.Equal("7", document.RootElement.GetProperty("values").GetProperty("metric").GetString());
        Assert.Equal("UNKNOWN", document.RootElement.GetProperty("values").GetProperty("segment_beta_ready").GetString());
        Assert.Equal("no-store", response.Headers.CacheControl!.ToString());
    }

    [Fact]
    public async Task Demo_E_roundtrips_domain_outputs_through_the_real_HTTP_contract()
    {
        var json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Cases", "demo-e-mixed.json"));
        var response = await Post("demo-e", json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var document = AssessmentJson.Deserialize(await response.Content.ReadAsStringAsync());
        Assert.Equal(5, document.Assessment.DomainOutputs.Count);
        Assert.Contains(
            document.Assessment.DomainOutputs,
            output => output.OutputId == "external_state"
                && output.Value.Choice == "PENDING_EXTERNAL");
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
