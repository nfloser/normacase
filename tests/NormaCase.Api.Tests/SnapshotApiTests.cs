using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc.Testing;
using NormaCase.Api;
using NormaCase.Serialization;
using Xunit;

namespace NormaCase.Api.Tests;

public sealed class SnapshotApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    public SnapshotApiTests(WebApplicationFactory<Program> factory) => _client = factory.CreateClient();

    [Theory]
    [InlineData("demo-a", "demo-a-supported")]
    [InlineData("demo-b", "demo-b-supported")]
    [InlineData("demo-c", "demo-c-review")]
    [InlineData("demo-d", "demo-d-supported")]
    [InlineData("demo-e", "demo-e-partial")]
    public async Task Capture_and_replay_match_the_existing_assessment_endpoint(string demo, string example)
    {
        var input = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Cases", example + ".json"));
        var capture = await Post("/api/snapshots/synthetic." + demo, input);
        Assert.Equal(HttpStatusCode.OK, capture.StatusCode);
        using var envelope = JsonDocument.Parse(await capture.Content.ReadAsStringAsync());
        var snapshot = envelope.RootElement.GetProperty("snapshotJson").GetString()!;
        var assessment = envelope.RootElement.GetProperty("assessmentJson").GetString()!;
        var restored = AssessmentSnapshotJson.Deserialize(snapshot);
        var expectedInput = CaseInputJson.Deserialize(input);
        Assert.Equal(expectedInput.AssessmentDate, restored.Input.AssessmentDate);
        Assert.Equal(expectedInput.Facts.Count, restored.Input.Facts.Count);
        foreach (var fact in expectedInput.Facts)
            Assert.Equal(fact.Value, restored.Input.Facts[fact.Key]);
        var direct = await Post("/api/assessments/synthetic." + demo, input);
        Assert.Equal(await direct.Content.ReadAsStringAsync(), assessment);
        var replay = await Post("/api/snapshots/replay", snapshot);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        using var verified = JsonDocument.Parse(await replay.Content.ReadAsStringAsync());
        Assert.Equal(assessment, verified.RootElement.GetProperty("assessmentJson").GetString());
        Assert.Equal("no-store", capture.Headers.CacheControl!.ToString());
        Assert.Equal("no-store", replay.Headers.CacheControl!.ToString());
    }

    [Fact]
    public async Task Invalid_checksum_and_wrong_platform_are_private_errors()
    {
        var snapshot = await Capture();
        var node = JsonNode.Parse(snapshot)!;
        node["contentSha256"] = new string('0', 64);
        var bad = await Post("/api/snapshots/replay", node.ToJsonString());
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        var original = AssessmentSnapshotJson.Deserialize(snapshot);
        var altered = AssessmentSnapshotJson.Serialize(original.KnowledgePackJson, original.Input,
            original.Assessment with { PlatformVersion = "private-version-marker" });
        var wrong = await Post("/api/snapshots/replay", altered);
        Assert.Equal(HttpStatusCode.Conflict, wrong.StatusCode);
        Assert.DoesNotContain("private-version-marker", await wrong.Content.ReadAsStringAsync());
        Assert.DoesNotContain("knowledgePackJson", await bad.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("/api/snapshots/synthetic.demo-a")]
    [InlineData("/api/snapshots/replay")]
    public async Task New_endpoints_reject_non_JSON_and_cross_origin_requests(string path)
    {
        var response = await _client.PostAsync(path, new StringContent("{}"));
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
        using var request = new HttpRequestMessage(HttpMethod.Post, path);
        request.Headers.Add("Origin", "https://example.invalid");
        request.Content = new StringContent("{}", Encoding.UTF8, "application/json");
        Assert.Equal(HttpStatusCode.Forbidden, (await _client.SendAsync(request)).StatusCode);
    }

    [Theory]
    [InlineData("/api/snapshots/synthetic.demo-a")]
    [InlineData("/api/snapshots/replay")]
    public async Task Chunked_unicode_body_is_bounded_in_bytes(string path)
    {
        using var content = new ChunkedContent(Encoding.UTF8.GetBytes(new string('ü', DemoHost.MaximumBodyBytes / 2 + 1)));
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, (await _client.PostAsync(path, content)).StatusCode);
    }

    [Fact]
    public async Task Unknown_capture_pack_is_not_loaded_from_a_path()
    {
        Assert.Equal(HttpStatusCode.NotFound,
            (await Post("/api/snapshots/unknown-secret", "{}")).StatusCode);
    }


    [Fact]
    public async Task Replay_accepts_only_synthetic_embedded_knowledge()
    {
        var original = AssessmentSnapshotJson.Deserialize(await Capture());
        var pack = JsonNode.Parse(original.KnowledgePackJson)!;
        pack["manifest"]!["validationLevel"] = "PUBLIC_REFERENCE";
        foreach (var source in pack["sources"]!.AsArray())
        {
            source!["retrievedAt"] = "2026-10-02";
            source["contentHash"] = "sha256:" + new string('0', 64);
        }
        var json = AssessmentSnapshotJson.Serialize(pack.ToJsonString(), original.Input, original.Assessment);
        var response = await Post("/api/snapshots/replay", json);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.DoesNotContain("knowledgePackJson", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Existing_endpoint_preserves_UTF8_BOM_compatibility()
    {
        var input = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Cases", "demo-a-supported.json"));
        var response = await Post("/api/assessments/synthetic.demo-a", "\uFEFF" + input);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Invalid_UTF8_is_rejected_without_decoder_details()
    {
        using var content = new ByteArrayContent(new byte[] { 0xff, 0xfe, 0xff });
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
        var response = await _client.PostAsync("/api/snapshots/replay", content);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.DoesNotContain("Decoder", await response.Content.ReadAsStringAsync());
    }

    private async Task<string> Capture()
    {
        var input = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Cases", "demo-a-supported.json"));
        var response = await Post("/api/snapshots/synthetic.demo-a", input);
        response.EnsureSuccessStatusCode();
        using var envelope = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return envelope.RootElement.GetProperty("snapshotJson").GetString()!;
    }

    private Task<HttpResponseMessage> Post(string path, string json)
        => _client.PostAsync(path, new StringContent(json, Encoding.UTF8, "application/json"));

    private sealed class ChunkedContent(byte[] bytes) : HttpContent
    {

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
            => stream.WriteAsync(bytes).AsTask();
        protected override bool TryComputeLength(out long length) { length = 0; return false; }
    }
}
