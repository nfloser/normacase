using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace NormaCase.Api;

internal sealed class ReviewAuthenticationOptions : AuthenticationSchemeOptions
{
    public byte[] CredentialHash { get; set; } = [];
}
internal sealed class ReviewAuthenticationHandler(IOptionsMonitor<ReviewAuthenticationOptions> options,
    ILoggerFactory logger, UrlEncoder encoder) : AuthenticationHandler<ReviewAuthenticationOptions>(options, logger, encoder)
{
    internal const string SchemeName = "SyntheticReviewBearer";
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var header = Request.Headers.Authorization;
        if (header.Count != 1 || !header[0]!.StartsWith("Bearer ", StringComparison.Ordinal))
            return Task.FromResult(AuthenticateResult.NoResult());
        var key = header[0]![7..];
        if (key.Length != 64 || !key.All(Uri.IsHexDigit)) return Task.FromResult(AuthenticateResult.Fail("Invalid synthetic credential."));
        var hash = SHA256.HashData(Convert.FromHexString(key));
        if (!CryptographicOperations.FixedTimeEquals(hash, Options.CredentialHash))
            return Task.FromResult(AuthenticateResult.Fail("Invalid synthetic credential."));
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "synthetic-local:reviewer"),
            new Claim("review_authority", "synthetic-local")], SchemeName);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
    }
    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = 401;
        Response.Headers.WWWAuthenticate = "Bearer";
        return Response.WriteAsJsonAsync(new { code = "review_login", message = ApiMessages.Get("review_login") });
    }
}
