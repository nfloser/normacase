using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using NormaCase.Api;
using NormaCase.Knowledge.Serialization;
using NormaCase.RuleEngine.Evaluation;
using NormaCase.Serialization;
using Xunit;

namespace NormaCase.Api.Tests;

public sealed class ApiHostTests
{
    [Fact]
    public async Task Host_binds_only_to_loopback_and_catalog_exposes_synthetic_contracts()
    {
        await using var host = await RunningApi.StartAsync();

        Assert.True(IPAddress.IsLoopback(IPAddress.Parse(host.BaseAddress.Host)));

        using var response = await host.Client.GetAsync("/api/v1/packs");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertSecurityHeaders(response);
        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var packs = document.RootElement.GetProperty("packs");

        var demoA = packs.EnumerateArray()
            .Single(item => item.GetProperty("packId").GetString() == "synthetic.demo-a");
        Assert.Equal("demo-a-2026.1", demoA.GetProperty("releaseId").GetString());
        Assert.Equal("SYNTHETIC", demoA.GetProperty("validationLevel").GetString());
        Assert.Contains(
            demoA.GetProperty("fields").EnumerateArray(),
            field => field.GetProperty("id").GetString() == "criterion_a"
                && field.GetProperty("type").GetString() == "truth"
                && field.GetProperty("required").GetBoolean());

        var demoC = packs.EnumerateArray()
            .Single(item => item.GetProperty("packId").GetString() == "synthetic.demo-c");
        Assert.Contains(
            demoC.GetProperty("evidenceRequirements").EnumerateArray(),
            evidence => evidence.GetProperty("id").GetString() == "verification");
    }

    [Fact]
    public async Task Api_result_is_lossless_engine_result_with_explicit_platform_identity()
    {
        await using var host = await RunningApi.StartAsync();
        const string json = """
            {
              "formatVersion": 1,
              "assessmentDate": "2026-10-02",
              "facts": {
                "criterion_a": { "kind": "TRUTH", "truth": "YES" },
                "criterion_b": { "kind": "TRUTH", "truth": "YES" },
                "criterion_c": { "kind": "TRUTH", "truth": "NO" }
              }
            }
            """;

        using var response = await host.Client.PostAsync(
            "/api/v1/packs/synthetic.demo-a/assessments",
            JsonContent(json));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertSecurityHeaders(response);

        var actual = await response.Content.ReadAsStringAsync();
        var pack = new KnowledgePackLoader().LoadFromFile(PackPath("demo-a"));
        var input = CaseInputJson.Deserialize(json);
        var assessment = new RuleEvaluator()
            .Evaluate(pack, input.Facts, input.AssessmentDate, input.Evidence);
        var expected = AssessmentJson.Serialize(assessment, RunningApi.PlatformVersion);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("example.com", null)]
    [InlineData(null, "https://example.com")]
    public async Task Non_local_host_or_origin_is_rejected(string? hostHeader, string? origin)
    {
        await using var host = await RunningApi.StartAsync();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/packs");
        if (hostHeader is not null)
            request.Headers.Host = hostHeader;
        if (origin is not null)
            request.Headers.Add("Origin", origin);

        using var response = await host.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        AssertSecurityHeaders(response);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("lokale", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("example.com", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Unsupported_media_type_is_rejected_deliberately()
    {
        await using var host = await RunningApi.StartAsync();
        using var content = new StringContent("{}", Encoding.UTF8, "text/plain");

        using var response = await host.Client.PostAsync(
            "/api/v1/packs/synthetic.demo-a/assessments",
            content);

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
        AssertSecurityHeaders(response);
        Assert.Contains(
            "application/json",
            await response.Content.ReadAsStringAsync(),
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Oversized_request_is_rejected_before_evaluation()
    {
        await using var host = await RunningApi.StartAsync();
        using var content = new ByteArrayContent(new byte[ApiHost.MaximumRequestBytes + 1]);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        using var response = await host.Client.PostAsync(
            "/api/v1/packs/synthetic.demo-a/assessments",
            content);

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        AssertSecurityHeaders(response);
    }

    [Fact]
    public async Task Invalid_input_is_German_and_does_not_echo_case_content()
    {
        await using var host = await RunningApi.StartAsync();
        const string secret = "secret_synthetic_marker";
        var json = $$"""
            {
              "formatVersion": 1,
              "assessmentDate": "2026-10-02",
              "facts": {},
              "{{secret}}": 98765
            }
            """;

        using var response = await host.Client.PostAsync(
            "/api/v1/packs/synthetic.demo-a/assessments",
            JsonContent(json));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        AssertSecurityHeaders(response);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("ungültig", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(secret, body, StringComparison.Ordinal);
        Assert.DoesNotContain("98765", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Unknown_pack_returns_not_found_without_path_details()
    {
        await using var host = await RunningApi.StartAsync();

        using var response = await host.Client.PostAsync(
            "/api/v1/packs/synthetic.does-not-exist/assessments",
            JsonContent("""{"formatVersion":1,"assessmentDate":"2026-10-02","facts":{}}"""));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        AssertSecurityHeaders(response);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("nicht verfügbar", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(AppContext.BaseDirectory, body, StringComparison.Ordinal);
    }

    private static StringContent JsonContent(string json)
        => new(json, Encoding.UTF8, "application/json");

    private static string PackPath(string demo)
        => Path.Combine(
            AppContext.BaseDirectory,
            "Knowledge",
            demo,
            "pack.json");

    private static void AssertSecurityHeaders(HttpResponseMessage response)
    {
        Assert.Contains("no-store", response.Headers.CacheControl?.ToString() ?? string.Empty);
        Assert.True(response.Headers.TryGetValues("X-Content-Type-Options", out var values));
        Assert.Contains("nosniff", values);
    }

    private sealed class RunningApi : IAsyncDisposable
    {
        public const string PlatformVersion = "api-test-1";

        private readonly WebApplication _application;

        private RunningApi(WebApplication application, HttpClient client, Uri baseAddress)
        {
            _application = application;
            Client = client;
            BaseAddress = baseAddress;
        }

        public HttpClient Client { get; }
        public Uri BaseAddress { get; }

        public static async Task<RunningApi> StartAsync()
        {
            var application = ApiHost.Build(new(
                KnowledgeRoot: Path.Combine(AppContext.BaseDirectory, "Knowledge"),
                PlatformVersion: PlatformVersion,
                Port: 0));

            await application.StartAsync();

            var server = application.Services.GetRequiredService<IServer>();
            var addresses = server.Features.Get<IServerAddressesFeature>()
                ?? throw new InvalidOperationException("Server addresses unavailable.");
            var address = new Uri(addresses.Addresses.Single());
            var client = new HttpClient { BaseAddress = address };

            return new(application, client, address);
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await _application.StopAsync();
            await _application.DisposeAsync();
        }
    }
}
