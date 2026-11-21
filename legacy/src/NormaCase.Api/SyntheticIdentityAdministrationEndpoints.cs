using System.Globalization;
using System.Text;
using System.Text.Json;
using NormaCase.Application.Authorization;

namespace NormaCase.Api;

internal static class SyntheticIdentityAdministrationEndpoints
{
    private const int MaximumReasonBytes = 4096;

    internal static void Map(WebApplication app)
    {
        var credential = app.Services.GetRequiredService<SyntheticReviewCredential>();
        var access = app.Services.GetRequiredService<SyntheticIdentityAccessGate>();
        var group = app.MapGroup("/api/review/administration").RequireAuthorization();

        group.MapGet("/identities", async (HttpContext context, CancellationToken token) =>
        {
            if (!TryAdministrator(context, credential, out _))
                return DemoHost.Error("review_forbidden", 403);
            var identities = new List<object>();
            foreach (var actorId in credential.ConfiguredUsers())
            {
                var state = await access.LoadAsync(actorId, token);
                identities.Add(State(state));
            }
            return Results.Json(new { identities });
        });

        group.MapGet("/identities/{actorId}", async (string actorId, HttpContext context, CancellationToken token) =>
        {
            if (!TryAdministrator(context, credential, out _))
                return DemoHost.Error("review_forbidden", 403);
            if (!credential.IsConfiguredUser(actorId))
                return DemoHost.Error("unknown_identity", 404);
            var state = await access.LoadAsync(actorId, token);
            var history = await access.LoadHistoryAsync(actorId, token);
            return Results.Json(new { state = State(state), history = history.Select(Audit).ToArray() });
        });

        group.MapPost("/identities/{actorId}/status", async (string actorId, HttpContext context, CancellationToken token) =>
        {
            if (!TryAdministrator(context, credential, out var administrator))
                return DemoHost.Error("review_forbidden", 403);
            if (!credential.IsConfiguredUser(actorId))
                return DemoHost.Error("unknown_identity", 404);
            var request = await ReadRequest(context.Request, token);
            if (request is null) return DemoHost.Error("invalid_input", 400);
            try
            {
                var changed = await access.ChangeAsync(new(
                    actorId, request.ExpectedRevision, request.Suspended,
                    administrator.ActorId, TimeProvider.System.GetUtcNow(), request.Reason), token);
                return Results.Json(State(changed));
            }
            catch (IdentityAccessConflictException)
            {
                return DemoHost.Error("identity_revision_conflict", 409);
            }
        });
    }

    private static bool TryAdministrator(
        HttpContext context,
        SyntheticReviewCredential credential,
        out NormaCase.Application.Reviews.AuthenticatedReviewActor actor)
    {
        actor = SyntheticReviewAuthentication.ResolveActor(context.User);
        return credential.IsAdministrator(actor);
    }

    private static object State(IdentityAccessState state) => new
    {
        actorId = state.ActorId,
        revision = state.Revision.ToString(CultureInfo.InvariantCulture),
        suspended = state.Suspended
    };

    private static object Audit(IdentityAccessAuditEntry entry) => new
    {
        actorId = entry.ActorId,
        revision = entry.Revision.ToString(CultureInfo.InvariantCulture),
        suspended = entry.Suspended,
        administratorActorId = entry.AdministratorActorId,
        changedAtUtc = entry.ChangedAtUtc.ToString("O", CultureInfo.InvariantCulture),
        reason = entry.Reason
    };

    private static async Task<StatusRequest?> ReadRequest(HttpRequest request, CancellationToken token)
    {
        if (!request.HasJsonContentType() || request.ContentLength is > MaximumReasonBytes)
            return null;
        try
        {
            using var stream = new MemoryStream();
            var buffer = new byte[1024];
            int read;
            while ((read = await request.Body.ReadAsync(buffer, token)) > 0)
            {
                if (stream.Length + read > MaximumReasonBytes) return null;
                stream.Write(buffer, 0, read);
            }
            using var json = JsonDocument.Parse(new UTF8Encoding(false, true).GetString(stream.ToArray()));
            var root = json.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || root.EnumerateObject().Select(item => item.Name).OrderBy(item => item, StringComparer.Ordinal)
                    .SequenceEqual(["expectedRevision", "reason", "suspended"]) is false
                || root.GetProperty("expectedRevision").ValueKind != JsonValueKind.String
                || !long.TryParse(root.GetProperty("expectedRevision").GetString(), NumberStyles.None,
                    CultureInfo.InvariantCulture, out var revision) || revision < 0
                || root.GetProperty("suspended").ValueKind is not (JsonValueKind.True or JsonValueKind.False)
                || root.GetProperty("reason").ValueKind != JsonValueKind.String)
                return null;
            var reason = root.GetProperty("reason").GetString()!;
            if (string.IsNullOrWhiteSpace(reason) || reason.Length > 1000 || reason.Any(char.IsControl)) return null;
            return new(revision, root.GetProperty("suspended").GetBoolean(), reason);
        }
        catch (Exception exception) when (exception is JsonException or DecoderFallbackException)
        {
            return null;
        }
    }

    private sealed record StatusRequest(long ExpectedRevision, bool Suspended, string Reason);
}
