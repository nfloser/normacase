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
        Assert.Equal(CaseInputJson.Deserialize(input).AssessmentDate, restored.Input.AssessmentDate);
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
        public ChunkedContent() : this([]) { }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
            => stream.WriteAsync(bytes).AsTask();
        protected override bool TryComputeLength(out long length) { length = 0; return false; }
    }
}
