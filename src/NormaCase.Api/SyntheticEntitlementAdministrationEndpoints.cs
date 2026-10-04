using System.Globalization;
using System.Text;
using System.Text.Json;
using NormaCase.Application.Authorization;
using NormaCase.Application.Reviews;

namespace NormaCase.Api;

internal static class SyntheticEntitlementAdministrationEndpoints
{
    private const int MaximumRequestBytes = 32 * 1024;

    internal static void Map(WebApplication app)
    {
        var credential = app.Services.GetRequiredService<SyntheticReviewCredential>();
        var store = app.Services.GetRequiredService<IReviewedEntitlementChangeStore>();
        var service = app.Services.GetRequiredService<ReviewedEntitlementChangeService>();
        var group = app.MapGroup("/api/review/administration/entitlement-changes").RequireAuthorization();

        group.MapGet("/context", async (HttpContext context, CancellationToken token) =>
        {
            if (!TryActor(context, credential.IsEntitlementProposer, out _))
                return DemoHost.Error("review_forbidden", 403);
            var identities = new List<object>();
            foreach (var actorId in credential.ConfiguredUsers())
            {
                var effective = await store.LoadEffectiveAsync(actorId, token);
                var configured = credential.ConfiguredEntitlements(actorId)!.Value;
                identities.Add(new
                {
                    actorId,
                    effectiveRevision = effective.Revision.ToString(CultureInfo.InvariantCulture),
                    actions = effective.Revision == 0 ? configured.Actions : effective.Actions,
                    caseIds = effective.Revision == 0 ? configured.CaseIds : effective.CaseIds,
                    availableCaseIds = credential.ConfiguredCases()
                });
            }
            return Results.Json(new { identities, authorizationActive = false });
        });

        group.MapPost("/", async (HttpContext context, CancellationToken token) =>
        {
            if (!TryActor(context, credential.IsEntitlementProposer, out var proposer))
                return DemoHost.Error("review_forbidden", 403);
            var request = await ReadProposal(context.Request, token);
            if (request is null || !credential.IsConfiguredUser(request.TargetActorId)
                || request.Actions.Any(action => !credential.IsConfiguredEntitlementAction(action))
                || request.CaseIds.Any(caseId => !credential.IsConfiguredCase(caseId))
                || (request.Actions.Any(action => action != "READ") && !request.Actions.Contains("READ", StringComparer.Ordinal)))
                return DemoHost.Error("invalid_input", 400);
            try
            {
                var record = await service.ProposeAsync(new(
                    request.ChangeId, request.TargetActorId, request.ExpectedEntitlementRevision,
                    request.Actions, request.CaseIds, proposer.ActorId,
                    TimeProvider.System.GetUtcNow(), request.Reason), token);
                return Results.Json(Change(record), statusCode: 201);
            }
            catch (EntitlementChangeConflictException)
            {
                return DemoHost.Error("entitlement_change_conflict", 409);
            }
            catch (ArgumentException)
            {
                return DemoHost.Error("invalid_input", 400);
            }
        });

        group.MapGet("/pending", async (HttpContext context, CancellationToken token) =>
        {
            if (!TryActor(context, credential.IsEntitlementApprover, out _))
                return DemoHost.Error("review_forbidden", 403);
            return Results.Json(new { changes = (await store.ListPendingAsync(100, token)).Select(Change).ToArray() });
        });

        group.MapPost("/{changeId}/decision", async (string changeId, HttpContext context, CancellationToken token) =>
        {
            if (!TryActor(context, credential.IsEntitlementApprover, out var approver))
                return DemoHost.Error("review_forbidden", 403);
            var request = await ReadDecision(context.Request, token);
            if (request is null) return DemoHost.Error("invalid_input", 400);
            try
            {
                var record = await service.DecideAsync(new(
                    changeId, approver.ActorId, TimeProvider.System.GetUtcNow(), request.Approved, request.Reason), token);
                return Results.Json(Change(record));
            }
            catch (EntitlementChangeNotFoundException)
            {
                return DemoHost.Error("entitlement_change_not_found", 404);
            }
            catch (Exception exception) when (exception is EntitlementChangeConflictException
                or EntitlementSeparationOfDutiesException)
            {
                return DemoHost.Error("entitlement_change_conflict", 409);
            }
            catch (ArgumentException)
            {
                return DemoHost.Error("invalid_input", 400);
            }
        });
    }

    private static bool TryActor(HttpContext context, Func<AuthenticatedReviewActor, bool> allowed,
        out AuthenticatedReviewActor actor)
    {
        actor = SyntheticReviewAuthentication.ResolveActor(context.User);
        return allowed(actor);
    }

    private static object Change(EntitlementChangeRecord record) => new
    {
        changeId = record.Proposal.ChangeId,
        targetActorId = record.Proposal.TargetActorId,
        expectedEntitlementRevision = record.Proposal.ExpectedEntitlementRevision.ToString(CultureInfo.InvariantCulture),
        actions = record.Proposal.Actions,
        caseIds = record.Proposal.CaseIds,
        proposerActorId = record.Proposal.ProposerActorId,
        proposedAtUtc = record.Proposal.ProposedAtUtc.ToString("O", CultureInfo.InvariantCulture),
        reason = record.Proposal.Reason,
        decision = record.Decision is null ? null : new
        {
            decisionActorId = record.Decision.DecisionActorId,
            decidedAtUtc = record.Decision.DecidedAtUtc.ToString("O", CultureInfo.InvariantCulture),
            approved = record.Decision.Approved,
            reason = record.Decision.Reason
        }
    };

    private static async Task<ProposalRequest?> ReadProposal(HttpRequest request, CancellationToken token)
    {
        var root = await ReadObject(request, token);
        if (root is null || !Exact(root.Value,
                "actions", "caseIds", "changeId", "expectedEntitlementRevision", "reason", "targetActorId")
            || !Text(root.Value, "changeId", out var changeId)
            || !Text(root.Value, "targetActorId", out var targetActorId)
            || !Revision(root.Value, "expectedEntitlementRevision", out var revision)
            || !Strings(root.Value, "actions", 32, out var actions)
            || !Strings(root.Value, "caseIds", 500, out var caseIds)
            || !Reason(root.Value, out var reason)) return null;
        return new(changeId, targetActorId, revision, actions, caseIds, reason);
    }

    private static async Task<DecisionRequest?> ReadDecision(HttpRequest request, CancellationToken token)
    {
        var root = await ReadObject(request, token);
        if (root is null || !Exact(root.Value, "approved", "reason")
            || root.Value.GetProperty("approved").ValueKind is not (JsonValueKind.True or JsonValueKind.False)
            || !Reason(root.Value, out var reason)) return null;
        return new(root.Value.GetProperty("approved").GetBoolean(), reason);
    }

    private static async Task<JsonElement?> ReadObject(HttpRequest request, CancellationToken token)
    {
        if (!request.HasJsonContentType() || request.ContentLength is > MaximumRequestBytes) return null;
        try
        {
            using var stream = new MemoryStream();
            var buffer = new byte[1024];
            int read;
            while ((read = await request.Body.ReadAsync(buffer, token)) > 0)
            {
                if (stream.Length + read > MaximumRequestBytes) return null;
                stream.Write(buffer, 0, read);
            }
            using var document = JsonDocument.Parse(new UTF8Encoding(false, true).GetString(stream.ToArray()));
            return document.RootElement.ValueKind == JsonValueKind.Object ? document.RootElement.Clone() : null;
        }
        catch (Exception exception) when (exception is JsonException or DecoderFallbackException) { return null; }
    }

    private static bool Exact(JsonElement root, params string[] properties)
        => root.EnumerateObject().Select(item => item.Name).OrderBy(item => item, StringComparer.Ordinal)
            .SequenceEqual(properties.OrderBy(item => item, StringComparer.Ordinal));
    private static bool Text(JsonElement root, string name, out string value)
    {
        value = root.GetProperty(name).ValueKind == JsonValueKind.String ? root.GetProperty(name).GetString()! : "";
        return value.Length != 0;
    }
    private static bool Revision(JsonElement root, string name, out long value)
        => root.GetProperty(name).ValueKind == JsonValueKind.String
            && long.TryParse(root.GetProperty(name).GetString(), NumberStyles.None, CultureInfo.InvariantCulture, out value)
            && value >= 0;
    private static bool Strings(JsonElement root, string name, int maximum, out string[] values)
    {
        values = [];
        var element = root.GetProperty(name);
        if (element.ValueKind != JsonValueKind.Array) return false;
        var supplied = element.EnumerateArray().ToArray();
        if (supplied.Length > maximum || supplied.Any(item => item.ValueKind != JsonValueKind.String)) return false;
        values = supplied.Select(item => item.GetString()!).ToArray();
        return values.Distinct(StringComparer.Ordinal).Count() == values.Length;
    }
    private static bool Reason(JsonElement root, out string reason)
    {
        reason = root.GetProperty("reason").ValueKind == JsonValueKind.String
            ? root.GetProperty("reason").GetString()! : "";
        return !string.IsNullOrWhiteSpace(reason) && reason.Length <= 1000 && !reason.Any(char.IsControl);
    }

    private sealed record ProposalRequest(string ChangeId, string TargetActorId, long ExpectedEntitlementRevision,
        string[] Actions, string[] CaseIds, string Reason);
    private sealed record DecisionRequest(bool Approved, string Reason);
}
