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
    private const string AdministratorActorId = "synthetic-local:administrator";
    private sealed record Entry(string ActorId, byte[] Credential, HashSet<string> Actions, HashSet<string> Cases, bool Administrator = false);
    private readonly Entry[] entries;
    private readonly bool legacy;
    private static readonly HashSet<string> ValidActions = ["READ", "ACCEPT", "OVERRIDE", "EXPORT", "INTAKE", "BATCH"];

    private SyntheticReviewCredential(bool enabled, Entry[] entries, bool legacy = false)
        => (Enabled, this.entries, this.legacy) = (enabled, entries, legacy);

    internal static SyntheticReviewCredential Load(IConfiguration configuration)
    {
        var mode = configuration["SyntheticReview:Enabled"];
        if (mode is not null && !bool.TryParse(mode, out _))
            throw new InvalidOperationException("Invalid synthetic review mode configuration.");
        var enabled = bool.TryParse(mode, out var parsed) && parsed;
        if (!enabled) return new(false, []);
        var users = configuration.GetSection("SyntheticReview:Users").GetChildren().ToArray();
        var administrator = configuration["SyntheticReview:Administrator:Credential"];
        if (users.Length == 0)
        {
            if (administrator is not null)
                throw new InvalidOperationException("Identity administration requires separate synthetic users.");
            if (!TryDecode(configuration["SyntheticReview:Credential"], out var bytes))
                throw new InvalidOperationException("Synthetic review requires an external canonical 256-bit credential.");
            return new(true, [new("synthetic-local:reviewer", bytes, [], [])], true);
        }
        if (users.Length > 100 || configuration["SyntheticReview:Credential"] is not null)
            throw new InvalidOperationException("Ambiguous synthetic identity configuration.");
        var entries = new List<Entry>();
        foreach (var user in users)
        {
            if (!System.Text.RegularExpressions.Regex.IsMatch(user.Key, "\\A[a-z][a-z0-9-]{0,63}\\z")
                || user.GetChildren().Any(item => item.Key is not ("Credential" or "Actions" or "CaseIds"))
                || !TryDecode(user["Credential"], out var bytes))
                throw new InvalidOperationException("Invalid synthetic identity configuration.");
            var actions = user.GetSection("Actions").GetChildren().Select(item => item.Value ?? "").ToHashSet(StringComparer.Ordinal);
            var cases = user.GetSection("CaseIds").GetChildren().Select(item => item.Value ?? "").ToHashSet(StringComparer.Ordinal);
            if (actions.Count == 0 || actions.Any(action => !ValidActions.Contains(action))
                || (actions.Any(action => action != "READ") && !actions.Contains("READ"))
                || cases.Count == 0 || cases.Count > 500 || cases.Any(id => !SyntheticReviewEndpoints.PermittedCaseId(id))
                || entries.Any(entry => CryptographicOperations.FixedTimeEquals(entry.Credential, bytes)))
                throw new InvalidOperationException("Invalid synthetic entitlement configuration.");
            entries.Add(new("synthetic-local:user-" + user.Key, bytes, actions, cases));
        }
        if (administrator is not null)
        {
            if (!configuration.GetValue<bool>("SyntheticReview:PersistenceEnabled")
                || !TryDecode(administrator, out var bytes)
                || entries.Any(entry => CryptographicOperations.FixedTimeEquals(entry.Credential, bytes)))
                throw new InvalidOperationException("Invalid synthetic administrator configuration.");
            entries.Add(new(AdministratorActorId, bytes, [], [], true));
        }
        return new(true, entries.ToArray());
    }

    internal bool TryAuthenticate(string token, out string actorId)
    {
        actorId = "";
        if (!Enabled || !TryDecode(token, out var candidate)) return false;
        try
        {
            // Compare every credential; do not stop at a matching user's position.
            foreach (var entry in entries)
                if (CryptographicOperations.FixedTimeEquals(entry.Credential, candidate)) actorId = entry.ActorId;
            return actorId.Length != 0;
        }
        finally { CryptographicOperations.ZeroMemory(candidate); }
    }

    internal bool Allows(AuthenticatedReviewActor actor, string caseId, string action)
        => actor.AuthenticationAuthority == "synthetic-local"
            && entries.Any(entry => entry.ActorId == actor.ActorId
                && (legacy || entry.Actions.Contains(action) && entry.Cases.Contains(caseId)));

    internal bool IsAdministrator(AuthenticatedReviewActor actor)
        => actor.AuthenticationAuthority == "synthetic-local"
            && actor.ActorId == AdministratorActorId
            && entries.Any(entry => entry.ActorId == actor.ActorId && entry.Administrator);

    internal bool IsConfiguredUser(string actorId)
        => entries.Any(entry => entry.ActorId == actorId && !entry.Administrator)
            && actorId != "synthetic-local:reviewer";

    internal IReadOnlyList<string> ConfiguredUsers()
        => entries.Where(entry => !entry.Administrator && entry.ActorId != "synthetic-local:reviewer")
            .Select(entry => entry.ActorId).OrderBy(id => id, StringComparer.Ordinal).ToArray();

    internal IReadOnlyCollection<NormaCase.Domain.Cases.CaseId>? ReadScope(AuthenticatedReviewActor actor)
    {
        var entry = entries.SingleOrDefault(item => item.ActorId == actor.ActorId);
        if (actor.AuthenticationAuthority != "synthetic-local" || entry is null) return [];
        return legacy ? null : entry.Actions.Contains("READ")
            ? entry.Cases.Select(id => new NormaCase.Domain.Cases.CaseId(id)).ToArray() : [];
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
    UrlEncoder encoder, SyntheticReviewCredential credential,
    SyntheticIdentityAccessGate accessGate)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    internal const string SchemeName = "SyntheticLocalReview";
    private const string Authority = "synthetic-local";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var headers = Request.Headers.Authorization;
        if (headers.Count == 0) return AuthenticateResult.NoResult();
        var value = headers.Count == 1 ? headers[0] : null;
        if (value is null || !value.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            || !credential.TryAuthenticate(value[7..], out var actorId))
            return AuthenticateResult.Fail("Invalid synthetic credential.");
        if (!await accessGate.IsActiveAsync(actorId, Context.RequestAborted))
            return AuthenticateResult.Fail("Synthetic identity is suspended.");
        var identity = new ClaimsIdentity(
            [new(ClaimTypes.NameIdentifier, actorId, ClaimValueTypes.String, Authority)], SchemeName);
        return AuthenticateResult.Success(
            new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName));
    }

    // Only claims issued by this verified scheme can cross the Application boundary.
    internal static AuthenticatedReviewActor ResolveActor(ClaimsPrincipal principal)
    {
        var identity = principal.Identities.SingleOrDefault(item =>
            item.IsAuthenticated && item.AuthenticationType == SchemeName);
        var subject = identity?.FindFirst(ClaimTypes.NameIdentifier);
        if (subject?.Issuer != Authority || string.IsNullOrWhiteSpace(subject.Value)
            || !subject.Value.StartsWith(Authority + ":", StringComparison.Ordinal))
            throw new InvalidOperationException("A verified synthetic review identity is required.");
        return new(subject.Value, Authority);
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.Headers.WWWAuthenticate = "Bearer";
        return DemoHost.Error("review_authentication_required", 401).ExecuteAsync(Context);
    }

    protected override Task HandleForbiddenAsync(AuthenticationProperties properties)
        => DemoHost.Error("review_forbidden", 403).ExecuteAsync(Context);
}
