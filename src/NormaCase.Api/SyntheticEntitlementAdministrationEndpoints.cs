using System.Globalization;
using System.Text;
using System.Text.Json;
using NormaCase.Application.Authorization;
using NormaCase.Persistence.PostgreSql;

namespace NormaCase.Api;

internal static class SyntheticEntitlementAdministrationEndpoints
{
    private const int MaximumRequestBytes = 64 * 1024;

    internal static void Map(WebApplication app)
    {
        var credential = app.Services.GetRequiredService<SyntheticReviewCredential>();
        var store = new PostgresReviewedEntitlementChangeStore(
            app.Services.GetRequiredService<Npgsql.NpgsqlDataSource>());
        var service = new ReviewedEntitlementChangeService(
            store, new(RequireDistinctDecisionActor: true));
        var group = app.MapGroup("/api/review/administration/entitlement-changes")
            .RequireAuthorization();

        group.MapGet("/catalog", (HttpContext context) =>
        {
            if (!TryAdministrator(context, credential, out _))
                return DemoHost.Error("review_forbidden", 403);
            return Results.Json(new
            {
                identities = credential.ConfiguredUsers(),
                actions = credential.KnownActions(),
                caseIds = credential.ConfiguredCaseIds()
            });
        });

        group.MapGet("/effective/{actorId}", async (
            string actorId, HttpContext context, CancellationToken token) =>
        {
            if (!TryAdministrator(context, credential, out _))
                return DemoHost.Error("review_forbidden", 403);
            if (!credential.IsConfiguredUser(actorId))
                return DemoHost.Error("unknown_identity", 404);
            var state = await store.LoadEffectiveAsync(actorId, token);
            return Results.Json(Effective(state));
        });

        group.MapGet("/pending", async (HttpContext context, CancellationToken token) =>
        {
            var actor = SyntheticReviewAuthentication.ResolveActor(context.User);
            if (!credential.IsAdministrator(actor) && !credential.IsEntitlementApprover(actor))
                return DemoHost.Error("review_forbidden", 403);
            var values = context.Request.Query["limit"];
            if (values.Count > 1)
                return DemoHost.Error("invalid_input", 400);
            var limit = 100;
            if (values.Count == 1
                && (!int.TryParse(values[0], NumberStyles.None, CultureInfo.InvariantCulture, out limit)
                    || limit is < 1 or > 100))
                return DemoHost.Error("invalid_input", 400);
            var pending = await service.ListPendingAsync(limit, token);
            return Results.Json(new { changes = pending.Select(Record).ToArray() });
        });

        group.MapPost("", async (HttpContext context, CancellationToken token) =>
        {
            if (!TryAdministrator(context, credential, out var administrator))
                return DemoHost.Error("review_forbidden", 403);
            var request = await ReadProposalRequest(context.Request, token);
            if (request is null)
                return DemoHost.Error("invalid_input", 400);
            if (!credential.IsConfiguredUser(request.TargetActorId))
                return DemoHost.Error("unknown_identity", 404);
            if (!credential.ValidEntitlementSnapshot(request.Actions, request.CaseIds))
                return DemoHost.Error("invalid_input", 400);

            try
            {
                var current = await store.LoadEffectiveAsync(request.TargetActorId, token);
                if (current.Revision != request.ExpectedEntitlementRevision)
                    return DemoHost.Error("entitlement_revision_conflict", 409);
                var proposal = new EntitlementChangeProposal(
                    "entitlement-" + Guid.NewGuid().ToString("N"),
                    request.TargetActorId,
                    request.ExpectedEntitlementRevision,
                    request.Actions,
                    request.CaseIds,
                    administrator.ActorId,
                    TimeProvider.System.GetUtcNow(),
                    request.Reason);
                return Results.Json(Record(await service.ProposeAsync(proposal, token)));
            }
            catch (ArgumentException)
            {
                return DemoHost.Error("invalid_input", 400);
            }
            catch (EntitlementChangeConflictException)
            {
                return DemoHost.Error("entitlement_revision_conflict", 409);
            }
        });

        group.MapPost("/{changeId}/decision", async (
            string changeId, HttpContext context, CancellationToken token) =>
        {
            var approver = SyntheticReviewAuthentication.ResolveActor(context.User);
            if (!credential.IsEntitlementApprover(approver))
                return DemoHost.Error("review_forbidden", 403);
            var request = await ReadDecisionRequest(context.Request, token);
            if (request is null)
                return DemoHost.Error("invalid_input", 400);
            try
            {
                var decision = new EntitlementChangeDecision(
                    changeId,
                    approver.ActorId,
                    TimeProvider.System.GetUtcNow(),
                    request.Approved,
                    request.Reason);
                return Results.Json(Record(await service.DecideAsync(decision, token)));
            }
            catch (ArgumentException)
            {
                return DemoHost.Error("invalid_input", 400);
            }
            catch (EntitlementChangeNotFoundException)
            {
                return DemoHost.Error("unknown_entitlement_change", 404);
            }
            catch (EntitlementSeparationOfDutiesException)
            {
                return DemoHost.Error("review_forbidden", 403);
            }
            catch (EntitlementChangeConflictException)
            {
                return DemoHost.Error("entitlement_revision_conflict", 409);
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

    private static object Record(EntitlementChangeRecord record) => new
    {
        proposal = new
        {
            changeId = record.Proposal.ChangeId,
            targetActorId = record.Proposal.TargetActorId,
            expectedEntitlementRevision =
                record.Proposal.ExpectedEntitlementRevision.ToString(CultureInfo.InvariantCulture),
            actions = record.Proposal.Actions,
            caseIds = record.Proposal.CaseIds,
            proposerActorId = record.Proposal.ProposerActorId,
            proposedAtUtc = record.Proposal.ProposedAtUtc.ToString("O", CultureInfo.InvariantCulture),
            reason = record.Proposal.Reason
        },
        decision = record.Decision is null ? null : new
        {
            changeId = record.Decision.ChangeId,
            decisionActorId = record.Decision.DecisionActorId,
            decidedAtUtc = record.Decision.DecidedAtUtc.ToString("O", CultureInfo.InvariantCulture),
            approved = record.Decision.Approved,
            reason = record.Decision.Reason
        }
    };

    private static object Effective(IdentityEntitlementState state) => new
    {
        actorId = state.ActorId,
        revision = state.Revision.ToString(CultureInfo.InvariantCulture),
        actions = state.Actions,
        caseIds = state.CaseIds
    };

    private static async Task<ProposalRequest?> ReadProposalRequest(
        HttpRequest request, CancellationToken token)
    {
        var root = await ReadObject(request, token);
        if (root is null
            || !ExactProperties(root.Value,
                "actions", "caseIds", "expectedEntitlementRevision", "reason", "targetActorId")
            || !TryString(root.Value, "targetActorId", out var target)
            || !TryRevision(root.Value, "expectedEntitlementRevision", out var revision)
            || !TryStringArray(root.Value, "actions", 32, out var actions)
            || !TryStringArray(root.Value, "caseIds", 500, out var cases)
            || !TryString(root.Value, "reason", out var reason)
            || string.IsNullOrWhiteSpace(reason) || reason.Length > 1000 || reason.Any(char.IsControl))
            return null;
        return new(target, revision, actions, cases, reason);
    }

    private static async Task<DecisionRequest?> ReadDecisionRequest(
        HttpRequest request, CancellationToken token)
    {
        var root = await ReadObject(request, token);
        if (root is null || !ExactProperties(root.Value, "approved", "reason")
            || !root.Value.TryGetProperty("approved", out var approved)
            || approved.ValueKind is not (JsonValueKind.True or JsonValueKind.False)
            || !TryString(root.Value, "reason", out var reason)
            || string.IsNullOrWhiteSpace(reason) || reason.Length > 1000 || reason.Any(char.IsControl))
            return null;
        return new(approved.GetBoolean(), reason);
    }

    private static async Task<JsonElement?> ReadObject(HttpRequest request, CancellationToken token)
    {
        if (!request.HasJsonContentType() || request.ContentLength is > MaximumRequestBytes)
            return null;
        try
        {
            using var memory = new MemoryStream();
            var buffer = new byte[4096];
            int read;
            while ((read = await request.Body.ReadAsync(buffer, token)) > 0)
            {
                if (memory.Length + read > MaximumRequestBytes) return null;
                memory.Write(buffer, 0, read);
            }
            using var document = JsonDocument.Parse(
                new UTF8Encoding(false, true).GetString(memory.ToArray()));
            return document.RootElement.ValueKind == JsonValueKind.Object
                ? document.RootElement.Clone()
                : null;
        }
        catch (Exception exception) when (exception is JsonException or DecoderFallbackException)
        {
            return null;
        }
    }

    private static bool ExactProperties(JsonElement root, params string[] expected)
        => root.EnumerateObject().Select(item => item.Name).OrderBy(item => item, StringComparer.Ordinal)
            .SequenceEqual(expected.OrderBy(item => item, StringComparer.Ordinal));

    private static bool TryString(JsonElement root, string name, out string value)
    {
        value = "";
        if (!root.TryGetProperty(name, out var element) || element.ValueKind != JsonValueKind.String)
            return false;
        value = element.GetString() ?? "";
        return true;
    }

    private static bool TryRevision(JsonElement root, string name, out long revision)
    {
        revision = 0;
        return TryString(root, name, out var value)
            && long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out revision)
            && revision >= 0;
    }

    private static bool TryStringArray(
        JsonElement root, string name, int maximum, out string[] values)
    {
        values = [];
        if (!root.TryGetProperty(name, out var element) || element.ValueKind != JsonValueKind.Array)
            return false;
        var items = element.EnumerateArray().ToArray();
        if (items.Length > maximum || items.Any(item => item.ValueKind != JsonValueKind.String))
            return false;
        values = items.Select(item => item.GetString() ?? "").ToArray();
        return values.All(value => value.Length != 0);
    }

    private sealed record ProposalRequest(
        string TargetActorId, long ExpectedEntitlementRevision,
        string[] Actions, string[] CaseIds, string Reason);
    private sealed record DecisionRequest(bool Approved, string Reason);
}
