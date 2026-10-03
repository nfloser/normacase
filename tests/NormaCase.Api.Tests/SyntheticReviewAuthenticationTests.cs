using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace NormaCase.Api.Tests;

public sealed class SyntheticReviewAuthenticationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string Credential = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";
    private readonly WebApplicationFactory<Program> factory;

    public SyntheticReviewAuthenticationTests(WebApplicationFactory<Program> factory)
        => this.factory = factory;

    [Fact]
    public async Task Review_endpoint_does_not_exist_in_default_read_only_preview()
    {
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/review-session")).StatusCode);
    }

    [Fact]
    public async Task Enabled_review_mode_requires_bearer_authentication_and_uses_server_actor()
    {
        using var configured = ConfiguredFactory();
        using var client = configured.CreateClient();

        var anonymous = await client.GetAsync("/api/review-session");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Equal("Bearer", anonymous.Headers.WwwAuthenticate.Single().Scheme);
        using (var error = JsonDocument.Parse(await anonymous.Content.ReadAsStringAsync()))
        {
            Assert.Equal("authentication_required", error.RootElement.GetProperty("code").GetString());
            Assert.Contains("Anmeldung", error.RootElement.GetProperty("message").GetString());
        }

        var wrong = new HttpRequestMessage(HttpMethod.Get, "/api/review-session");
        wrong.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            Base64Url(Enumerable.Repeat((byte)1, 32).ToArray()));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(wrong)).StatusCode);

        var valid = new HttpRequestMessage(HttpMethod.Get, "/api/review-session");
        valid.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Credential);
        var response = await client.SendAsync(valid);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl!.ToString());
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("synthetic-reviewer", document.RootElement.GetProperty("actorId").GetString());
        Assert.Equal("SYNTHETIC_REVIEW", document.RootElement.GetProperty("mode").GetString());
        Assert.Equal("POSTGRESQL", document.RootElement.GetProperty("storage").GetString());
    }

    [Fact]
    public void Enabled_review_mode_rejects_weak_or_missing_security_configuration()
    {
        var weak = new NormaCase.Api.SyntheticReviewHostOptions
        {
            Enabled = true,
            BearerToken = "too-short",
            ActorId = "synthetic-reviewer",
            ConnectionString = "Host=localhost;Database=normacase"
        };
        Assert.Throws<InvalidOperationException>(() => weak.ValidateAndDecodeCredential());

        var missingDatabase = new NormaCase.Api.SyntheticReviewHostOptions
        {
            Enabled = true,
            BearerToken = Credential,
            ActorId = "synthetic-reviewer"
        };
        Assert.Throws<InvalidOperationException>(() => missingDatabase.ValidateAndDecodeCredential());
    }

    private WebApplicationFactory<Program> ConfiguredFactory()
        => factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("SyntheticReview:Enabled", "true");
            builder.UseSetting("SyntheticReview:BearerToken", Credential);
            builder.UseSetting("SyntheticReview:ActorId", "synthetic-reviewer");
            builder.UseSetting(
                "ConnectionStrings:SyntheticReview",
                "Host=localhost;Database=normacase;Username=normacase;Password=synthetic-only");
        });

    private static string Base64Url(byte[] value)
        => Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
