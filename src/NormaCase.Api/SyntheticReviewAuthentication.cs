using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using NormaCase.Application.Reviews;

namespace NormaCase.Api;

// A local synthetic bootstrap identity, never an organizational identity provider.
internal sealed class SyntheticReviewCredential
{
    internal bool Enabled { get; }
    private readonly byte[] credential;

    private SyntheticReviewCredential(bool enabled, byte[] credential)
        => (Enabled, this.credential) = (enabled, credential);

    internal static SyntheticReviewCredential Load(IConfiguration configuration)
    {
        var mode = configuration["SyntheticReview:Enabled"];
        if (mode is not null && !bool.TryParse(mode, out _))
            throw new InvalidOperationException("Invalid synthetic review mode configuration.");
        var enabled = bool.TryParse(mode, out var parsed) && parsed;
        if (!enabled) return new(false, []);
        if (!TryDecode(configuration["SyntheticReview:Credential"], out var bytes))
            throw new InvalidOperationException("Synthetic review requires an external canonical 256-bit credential.");
        return new(true, bytes);
    }

    internal bool Matches(string token)
    {
        if (!Enabled || !TryDecode(token, out var candidate)) return false;
        try { return CryptographicOperations.FixedTimeEquals(credential, candidate); }
        finally { CryptographicOperations.ZeroMemory(candidate); }
    }

    private static bool TryDecode(string? token, out byte[] bytes)
    {
        bytes = new byte[32];
        return token is { Length: 44 }
            && Convert.TryFromBase64String(token, bytes, out var written) && written == 32
            && Convert.ToBase64String(bytes).Equals(token, StringComparison.Ordinal);
    }
}

internal sealed class SyntheticReviewAuthentication(
    IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger,
    UrlEncoder encoder, SyntheticReviewCredential credential)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    internal const string SchemeName = "SyntheticLocalReview";
    private const string Authority = "synthetic-local";
    private const string ActorId = Authority + ":reviewer";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var headers = Request.Headers.Authorization;
        if (headers.Count == 0) return Task.FromResult(AuthenticateResult.NoResult());
        var value = headers.Count == 1 ? headers[0] : null;
        if (value is null || !value.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            || !credential.Matches(value[7..]))
            return Task.FromResult(AuthenticateResult.Fail("Invalid synthetic credential."));
        var identity = new ClaimsIdentity(
            [new(ClaimTypes.NameIdentifier, ActorId, ClaimValueTypes.String, Authority)], SchemeName);
        return Task.FromResult(AuthenticateResult.Success(
            new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
    }

    // Only claims issued by this verified scheme can cross the Application boundary.
    internal static AuthenticatedReviewActor ResolveActor(ClaimsPrincipal principal)
    {
        var identity = principal.Identities.SingleOrDefault(item =>
            item.IsAuthenticated && item.AuthenticationType == SchemeName);
        var subject = identity?.FindFirst(ClaimTypes.NameIdentifier);
        if (subject?.Issuer != Authority || subject.Value != ActorId)
            throw new InvalidOperationException("A verified synthetic review identity is required.");
        return new(ActorId, Authority);
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.Headers.WWWAuthenticate = "Bearer";
        return DemoHost.Error("review_authentication_required", 401).ExecuteAsync(Context);
    }

    protected override Task HandleForbiddenAsync(AuthenticationProperties properties)
        => DemoHost.Error("review_forbidden", 403).ExecuteAsync(Context);
}
