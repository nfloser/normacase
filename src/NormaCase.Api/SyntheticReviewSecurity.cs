using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace NormaCase.Api;

public sealed class SyntheticReviewHostOptions
{
    public const string SectionName = "SyntheticReview";

    public bool Enabled { get; init; }
    public string? BearerToken { get; init; }
    public string? ActorId { get; init; }
    public string? ConnectionString { get; init; }

    public static SyntheticReviewHostOptions FromConfiguration(IConfiguration configuration)
        => new()
        {
            Enabled = configuration.GetValue<bool>($"{SectionName}:Enabled"),
            BearerToken = configuration[$"{SectionName}:BearerToken"],
            ActorId = configuration[$"{SectionName}:ActorId"],
            ConnectionString = configuration.GetConnectionString("SyntheticReview")
        };

    public byte[] ValidateAndDecodeCredential()
    {
        if (!Enabled)
            return Array.Empty<byte>();

        if (string.IsNullOrWhiteSpace(ActorId)
            || ActorId.Length > 128
            || !string.Equals(ActorId, ActorId.Trim(), StringComparison.Ordinal)
            || ActorId.Any(char.IsControl))
            throw new InvalidOperationException("Synthetic review actor configuration is invalid.");

        if (string.IsNullOrWhiteSpace(ConnectionString))
            throw new InvalidOperationException("Synthetic review PostgreSQL configuration is required.");

        if (!TryDecodeCredential(BearerToken, out var credential))
            throw new InvalidOperationException("Synthetic review bearer credential must be a 256-bit base64url value.");

        return credential;
    }

    internal static bool TryDecodeCredential(string? value, out byte[] credential)
    {
        credential = Array.Empty<byte>();
        if (string.IsNullOrEmpty(value) || !string.Equals(value, value.Trim(), StringComparison.Ordinal))
            return false;

        try
        {
            var normalized = value.Replace('-', '+').Replace('_', '/');
            normalized = normalized.Length % 4 switch
            {
                0 => normalized,
                2 => normalized + "==",
                3 => normalized + "=",
                _ => string.Empty
            };
            if (normalized.Length == 0)
                return false;

            var bytes = Convert.FromBase64String(normalized);
            if (bytes.Length != 32)
            {
                CryptographicOperations.ZeroMemory(bytes);
                return false;
            }

            credential = bytes;
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}

internal sealed class SyntheticReviewCredential
{
    public SyntheticReviewCredential(SyntheticReviewHostOptions options)
    {
        ActorId = options.ActorId!;
        Bytes = options.ValidateAndDecodeCredential();
    }

    public string ActorId { get; }
    public byte[] Bytes { get; }
}

internal sealed class SyntheticReviewAuthenticationHandler
    : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string Scheme = "SyntheticReviewBearer";
    private readonly SyntheticReviewCredential credential;

    public SyntheticReviewAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        SyntheticReviewCredential credential)
        : base(options, logger, encoder)
    {
        this.credential = credential;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("Authorization", out var values) || values.Count != 1)
            return Task.FromResult(AuthenticateResult.NoResult());

        var header = values[0];
        const string prefix = "Bearer ";
        if (header is null || !header.StartsWith(prefix, StringComparison.Ordinal))
            return Task.FromResult(AuthenticateResult.Fail("Invalid bearer credential."));

        var suppliedText = header[prefix.Length..];
        if (!SyntheticReviewHostOptions.TryDecodeCredential(suppliedText, out var supplied))
            return Task.FromResult(AuthenticateResult.Fail("Invalid bearer credential."));

        try
        {
            if (!CryptographicOperations.FixedTimeEquals(credential.Bytes, supplied))
                return Task.FromResult(AuthenticateResult.Fail("Invalid bearer credential."));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(supplied);
        }

        var identity = new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.NameIdentifier, credential.ActorId) },
            Scheme);
        var principal = new ClaimsPrincipal(identity);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme)));
    }

    protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.Headers.WWWAuthenticate = "Bearer";
        await DemoHost.Error("authentication_required", StatusCodes.Status401Unauthorized).ExecuteAsync(Context);
    }

    protected override Task HandleForbiddenAsync(AuthenticationProperties properties)
        => DemoHost.Error("authorization_denied", StatusCodes.Status403Forbidden).ExecuteAsync(Context);
}
