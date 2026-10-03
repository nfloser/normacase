using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace NormaCase.Api.Tests;

public sealed class SyntheticAuthenticationTests
{
    private static readonly string Token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    private static WebApplicationFactory<Program> Host(string? credential = null, string enabled = "true")
        => new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["SyntheticReview:Enabled"] = enabled,
                    ["SyntheticReview:Credential"] = credential ?? Token,
                    ["NORMACASE_REVIEW_DEMO_CONNECTION"] = "Host=localhost;Database=synthetic-unused"
                })));

    [Fact]
    public async Task Default_preview_has_no_authenticated_identity_endpoint()
    {
        await using var host = new WebApplicationFactory<Program>();
        using var client = host.CreateClient();
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/review-session")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/work-queues")).StatusCode);
    }

    [Fact]
    public async Task Verified_identity_is_server_owned_and_credentials_are_not_returned()
    {
        await using var host = Host();
        using var client = host.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/review-session?actorId=forged");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token);
        request.Headers.Add("X-Actor-Id", "forged");
        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl!.ToString());
        var text = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(text);
        Assert.Equal("synthetic-local:reviewer", json.RootElement.GetProperty("actorId").GetString());
        Assert.DoesNotContain(Token, text);
        Assert.DoesNotContain("forged", text);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Basic abc")]
    [InlineData("Bearer invalid")]
    [InlineData("Bearer ")]
    [InlineData("Bearer AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAB=")]
    [InlineData("Bearer AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA= ")]
    public async Task Missing_or_malformed_authorization_is_denied(string? authorization)
    {
        await using var host = Host();
        using var client = host.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/review-session?access_token=" + Uri.EscapeDataString(Token));
        if (authorization is not null) request.Headers.TryAddWithoutValidation("Authorization", authorization);
        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("Bearer", response.Headers.WwwAuthenticate.Single().Scheme);
        Assert.Contains("Anmeldung", await response.Content.ReadAsStringAsync());
        Assert.Equal("no-store", response.Headers.CacheControl!.ToString());
    }

    [Fact]
    public async Task Wrong_valid_token_duplicate_header_and_cross_origin_are_denied()
    {
        await using var host = Host();
        using var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/review-session")).StatusCode);
        client.DefaultRequestHeaders.Clear();
        client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", new[] { "Bearer " + Token, "Bearer " + Token });
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/review-session")).StatusCode);
        client.DefaultRequestHeaders.Clear();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token);
        client.DefaultRequestHeaders.Add("Origin", "https://example.org");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/review-session")).StatusCode);
    }

    [Fact]
    public async Task Explicitly_disabled_mode_ignores_unused_credential()
    {
        await using var host = Host("invalid-unused-key", "false");
        using var client = host.CreateClient();
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/review-session")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/work-queues")).StatusCode);
    }

    [Fact]
    public async Task Misspelled_mode_is_not_silently_enabled_or_disabled()
    {
        await using var host = Host(enabled: "tru");
        Assert.ThrowsAny<Exception>(() => host.CreateClient());
    }

    [Theory]
    [InlineData("")]
    [InlineData("short")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAB=")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA= ")]
    public async Task Enabled_host_rejects_invalid_configuration(string credential)
    {
        await using var host = Host(credential);
        Assert.ThrowsAny<Exception>(() => host.CreateClient());
    }
}
