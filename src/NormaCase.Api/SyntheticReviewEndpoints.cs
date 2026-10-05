using System.Globalization;
using System.Text;
using System.Text.Json;
using Npgsql;
using NormaCase.Application.Assessments;
using NormaCase.Application.Processing;
using NormaCase.Application.Reviews;
using NormaCase.Application.Triage;
using NormaCase.Application.WorkQueues;
using NormaCase.Domain.Audit;
using NormaCase.Domain.Cases;
using NormaCase.Domain.Decision;
using NormaCase.Domain.Workflow;
using NormaCase.Knowledge.Model;
using NormaCase.Persistence.PostgreSql;
using NormaCase.Serialization;

namespace NormaCase.Api;

internal static class SyntheticReviewEndpoints
{
    private static readonly string[] CaseIds =
        ["demo-g-supported", "demo-g-not-supported", "demo-g-incomplete", "demo-g-review",
         "demo-g-batch-supported", "demo-g-batch-not-supported"];

    internal static readonly WorkflowDefinition Workflow = new(
        "synthetic-reviewed-queue-process", 1, "received",
        [new("received", false), new("awaiting-approval", false), new("waiting-information", false),
         new("manual-review", false), new("accepted", true), new("overridden", true)],
        [new("prepare-approval", "received", "awaiting-approval"),
         new("request-information", "received", "waiting-information"),
         new("request-review", "received", "manual-review"),
         new("accept-review", "awaiting-approval", "accepted"),
         new("override-review", "awaiting-approval", "overridden")]);

    internal static readonly ApprovalRoutingPolicy TriagePolicy = new(
        "synthetic-reviewed-triage", 1,
        [AssessmentOutcome.Supported, AssessmentOutcome.NotSupported]);

    internal static readonly CaseProcessingRoutingPolicy RoutingPolicy = new(
        "synthetic-reviewed-routing", 1,
        TriagePolicy.Id, TriagePolicy.Version, Workflow.Id, Workflow.Version,
        new Dictionary<AssessmentRoutingDisposition, string>
        {
            [AssessmentRoutingDisposition.ReadyForApproval] = "prepare-approval",
            [AssessmentRoutingDisposition.Incomplete] = "request-information",
            [AssessmentRoutingDisposition.HumanReview] = "request-review"
        });

    private static readonly CaseWorkQueueConfiguration QueueConfiguration = new(
        "synthetic-reviewed-queues", 1,
        [new("approval", Workflow.Id, Workflow.Version, ["awaiting-approval"]),
         new("clarification", Workflow.Id, Workflow.Version, ["waiting-information"]),
         new("review", Workflow.Id, Workflow.Version, ["manual-review"]),
         new("completed", Workflow.Id, Workflow.Version, ["accepted", "overridden"])]);

    internal static readonly CaseReviewPolicy ReviewPolicy = new(
        "synthetic-reviewed-policy", 1, Workflow.Id, Workflow.Version,
        new Dictionary<HumanReviewDisposition, string>
        {
            [HumanReviewDisposition.AcceptSystemResult] = "accept-review",
            [HumanReviewDisposition.Override] = "override-review"
        });

    internal static void Map(
        WebApplication app,
        IReadOnlyDictionary<string, KnowledgePack> packs,
        string platformVersion)
    {
        var source = app.Services.GetRequiredService<NpgsqlDataSource>();
        var store = new PostgresCaseReviewStore(source, ResolveWorkflow);
        Seed(source, store, packs, platformVersion).GetAwaiter().GetResult();
        SyntheticRoundtripEndpoints.Map(app, source, store, packs["synthetic.demo-g"], platformVersion);

        var entitlements = app.Services.GetRequiredService<SyntheticLiveEntitlements>();
        var group = app.MapGroup("/api/review").RequireAuthorization();
        SyntheticBatchReviewEndpoints.Map(group, source, store, entitlements);

        group.MapGet("/work-queues", async (HttpContext context, CancellationToken token) =>
        {
            var actor = SyntheticReviewAuthentication.ResolveActor(context.User);
            var sizeText = context.Request.Query["pageSize"];
            var cursorText = context.Request.Query["afterCaseId"];
            if (sizeText.Count > 1 || cursorText.Count > 1) return DemoHost.Error("invalid_input", 400);
            var size = 100;
            if (sizeText.Count != 0 && (!int.TryParse(sizeText[0], NumberStyles.None, CultureInfo.InvariantCulture, out size) || size is < 1 or > 100))
                return DemoHost.Error("invalid_input", 400);
            var cursor = cursorText.Count == 0 ? null : cursorText[0];
            if (cursor is not null && !PermittedCaseId(cursor)) return DemoHost.Error("invalid_input", 400);
            return await entitlements.ExecuteAuthorizedAsync<IResult>(actor, async (entitlement, lockedToken) =>
            {
                var ids = await store.ListCasePageAsync(size, cursor, entitlement.ReadScope(), lockedToken);
                var page = ids.Take(size).ToArray();
                var states = new List<CaseReviewState>();
                foreach (var id in page)
                {
                    if (!PermittedCaseId(id.Value) || !entitlement.Allows(id.Value, "READ")) continue;
                    states.Add(await store.LoadAsync(id, lockedToken) ?? throw new CaseReviewIntegrityException());
                }
                var projection = new CaseWorkQueueProjectionService();
                return Results.Json(new
                {
                    configurationId = QueueConfiguration.Id,
                    configurationVersion = QueueConfiguration.Version,
                    nextPageCursor = ids.Count > size ? page[^1].Value : null,
                    queues = QueueConfiguration.Queues.Select(queue => new
                    {
                        queueId = queue.QueueId,
                        items = states
                            .Select(state => (State: state, Membership: projection.Project(state.Process, QueueConfiguration)))
                            .Where(item => item.Membership.QueueId == queue.QueueId)
                            .Select(item => new
                            {
                                caseId = item.State.Process.CaseId.Value,
                                caseRevision = item.State.Process.CaseRevision.ToString(CultureInfo.InvariantCulture),
                                processRevision = item.State.Process.Revision.ToString(CultureInfo.InvariantCulture),
                                stateId = item.State.Process.StateId,
                                assessmentId = item.State.Assessment.AssessmentId.Value,
                                auditRevision = item.State.Audit.Events[^1].Sequence.ToString(CultureInfo.InvariantCulture),
                                batchAllowed = item.State.Process.StateId == "awaiting-approval"
                                    && entitlement.Allows(item.State.Process.CaseId.Value, "ACCEPT")
                                    && entitlement.Allows(item.State.Process.CaseId.Value, "BATCH")
                            }).ToArray()
                    }).ToArray()
                });
            }, token);
        });

        group.MapGet("/work-cases/{caseId}", async (string caseId, HttpContext context, CancellationToken token) =>
        {
            var actor = SyntheticReviewAuthentication.ResolveActor(context.User);
            return await entitlements.ExecuteAuthorizedAsync<IResult>(actor, async (entitlement, lockedToken) =>
            {
                if (!PermittedCaseId(caseId) || !entitlement.Allows(caseId, "READ"))
                    return DemoHost.Error("unknown_work_case", 404);
                var state = await store.LoadAsync(new(caseId), lockedToken);
                return state is null ? DemoHost.Error("unknown_work_case", 404)
                    : Results.Json(Detail(state, entitlement));
            }, token);
        });

        group.MapPost("/work-cases/{caseId}/reviews", async (string caseId, HttpContext context, CancellationToken token) =>
        {
            var parsed = await ReadReviewRequest(context.Request, token);
            if (parsed.Error is not null) return parsed.Error;
            var request = parsed.Request!;

            AuthenticatedReviewActor actor;
            try
            {
                actor = SyntheticReviewAuthentication.ResolveActor(context.User);
            }
            catch (InvalidOperationException)
            {
                return DemoHost.Error("review_forbidden", 403);
            }

            try
            {
                return await entitlements.ExecuteAuthorizedAsync<IResult>(actor, async (entitlement, lockedToken) =>
                {
                    if (!PermittedCaseId(caseId) || !entitlement.Allows(caseId, "READ"))
                        return DemoHost.Error("unknown_work_case", 404);
                    var current = await store.LoadAsync(new(caseId), lockedToken);
                    if (current is null) return DemoHost.Error("unknown_work_case", 404);
                    var disposition = request.Disposition == "ACCEPT_SYSTEM_RESULT"
                        ? HumanReviewDisposition.AcceptSystemResult : HumanReviewDisposition.Override;
                    var command = new CaseReviewCommand(current.Process.CaseId, current.Assessment.AssessmentId,
                        new("synthetic-review-" + Guid.NewGuid().ToString("N")), request.ExpectedCaseRevision,
                        request.ExpectedProcessRevision, request.ExpectedAuditRevision, TimeProvider.System.GetUtcNow(),
                        disposition, request.Reason, request.OverrideOutcome);
                    var service = new CaseReviewService(store, new SyntheticAuthorizer(entitlement));
                    var committed = await service.ReviewAsync(actor, command, Workflow, ReviewPolicy, lockedToken);
                    return Results.Json(Detail(committed, entitlement));
                }, token);
            }
            catch (CaseReviewDeniedException)
            {
                return DemoHost.Error("review_forbidden", 403);
            }
            catch (CaseReviewConflictException)
            {
                return DemoHost.Error("review_conflict", 409);
            }
            catch (CaseReviewPolicyException)
            {
                return DemoHost.Error("review_forbidden", 403);
            }
            catch (ArgumentException)
            {
                return DemoHost.Error("invalid_input", 400);
            }
        });
    }

    private static async Task Seed(
        NpgsqlDataSource source,
        PostgresCaseReviewStore store,
        IReadOnlyDictionary<string, KnowledgePack> packs,
        string platformVersion)
    {
        var assessmentStore = new PostgresAssessmentRecordStore(source);
        var examplesDirectory = Path.Combine(AppContext.BaseDirectory, "Examples");
        var inputJsonByCaseId = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["demo-g-supported"] = File.ReadAllText(Path.Combine(examplesDirectory, "demo-g-supported.json")),
            ["demo-g-not-supported"] = File.ReadAllText(Path.Combine(examplesDirectory, "demo-g-not-supported.json")),
            ["demo-g-incomplete"] = File.ReadAllText(Path.Combine(examplesDirectory, "demo-g-incomplete.json")),
            ["demo-g-review"] = File.ReadAllText(Path.Combine(examplesDirectory, "demo-g-review.json")),
            ["demo-g-batch-supported"] = File.ReadAllText(Path.Combine(examplesDirectory, "demo-g-supported.json")),
            ["demo-g-batch-not-supported"] = File.ReadAllText(Path.Combine(examplesDirectory, "demo-g-not-supported.json"))
        };
        foreach (var id in CaseIds)
        {
            var caseId = new CaseId(id);
            var assessmentId = new AssessmentId("assessment-" + id);

            // A committed aggregate is historical state. Never re-evaluate it with the
            // currently running platform/knowledge merely to seed the synthetic host.
            var storedState = await store.LoadAsync(caseId);
            if (storedState is not null)
            {
                if (storedState.Assessment.CaseId != caseId
                    || storedState.Assessment.AssessmentId != assessmentId
                    || !string.Equals(storedState.Assessment.KnowledgePackId, "synthetic.demo-g", StringComparison.Ordinal))
                    throw new CaseReviewIntegrityException();
                continue;
            }

            var record = await assessmentStore.LoadAsync(assessmentId);
            if (record is null)
            {
                var input = CaseInputJson.Deserialize(inputJsonByCaseId[id]);
                record = new AssessmentRecorder().Evaluate(
                    packs["synthetic.demo-g"], input.Facts, input.AssessmentDate, input.Evidence,
                    new(assessmentId, caseId, platformVersion,
                        new DateTimeOffset(2026, 10, 3, 13, 0, 0, TimeSpan.Zero)));
                await assessmentStore.AppendAsync(record);
            }
            else if (record.CaseId != caseId
                || !string.Equals(record.KnowledgePackId, "synthetic.demo-g", StringComparison.Ordinal))
            {
                throw new CaseReviewIntegrityException();
            }

            var triage = new AssessmentTriageService().Route(record, TriagePolicy);
            var process = CaseProcessingInstance.Start(record.CaseId, 1, Workflow);
            var routed = new CaseProcessingRoutingService().Apply(
                triage, process, Workflow, RoutingPolicy, 1, 0);
            if (routed.Status != CaseProcessingRoutingStatus.Applied)
                throw new InvalidOperationException("Synthetic reviewed fixture could not be routed.");

            var expected = new CaseReviewState(
                record,
                1,
                routed.Process,
                AssessmentAuditTrail.Start(AssessmentAuditEvent.AssessmentCreated(
                    1, record.AssessmentId, record.RecordedAtUtc, "synthetic-seed")));

            await store.InitializeAsync(expected);
        }
    }

    internal static object Detail(CaseReviewState state, SyntheticEntitlementSnapshot? entitlement = null)
        => new
        {
            caseId = state.Process.CaseId.Value,
            caseRevision = state.Process.CaseRevision.ToString(CultureInfo.InvariantCulture),
            processRevision = state.Process.Revision.ToString(CultureInfo.InvariantCulture),
            stateId = state.Process.StateId,
            assessmentId = state.Assessment.AssessmentId.Value,
            packId = state.Assessment.KnowledgePackId,
            assessmentJson = AssessmentJson.Serialize(state.Assessment.Result, state.Assessment.PlatformVersion),
            evidence = state.Assessment.Input.Evidence.ToDictionary(
                item => item.Key, item => item.Value.ToString().ToUpperInvariant(), StringComparer.Ordinal),
            allowedActions = AllowedActions(state).Where(action => entitlement is null
                || entitlement.Allows(state.Process.CaseId.Value,
                    action == "ACCEPT_SYSTEM_RESULT" ? "ACCEPT" : "OVERRIDE")).ToArray(),
            auditRevision = state.Audit.Events[^1].Sequence.ToString(CultureInfo.InvariantCulture),
            audit = state.Audit.Events.Select(item => new
            {
                sequence = item.Sequence.ToString(CultureInfo.InvariantCulture),
                kind = item.Kind == AuditEventKind.AssessmentCreated ? "ASSESSMENT_CREATED" : "HUMAN_REVIEW_RECORDED",
                occurredAtUtc = item.OccurredAt,
                actorId = item.ActorId,
                disposition = item.Review is null ? null : DispositionText(item.Review.Disposition),
                reason = item.Review?.Reason,
                overrideOutcome = item.Review?.OverrideOutcome?.ToString().ToUpperInvariant()
            }).ToArray()
        };

    internal static bool PermittedCaseId(string id)
        => CaseIds.Contains(id, StringComparer.Ordinal)
            || System.Text.RegularExpressions.Regex.IsMatch(id, "\\Asynthetic-intake-[0-9a-f]{64}\\z", System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    private static string[] AllowedActions(CaseReviewState state)
    {
        if (state.Process.StateId != "awaiting-approval") return [];
        return ["ACCEPT_SYSTEM_RESULT", "OVERRIDE"];
    }

    private static string DispositionText(HumanReviewDisposition disposition)
        => disposition == HumanReviewDisposition.AcceptSystemResult ? "ACCEPT_SYSTEM_RESULT" : "OVERRIDE";

    internal static WorkflowDefinition ResolveWorkflow(string id, int version)
        => id == Workflow.Id && version == Workflow.Version
            ? Workflow
            : throw new ArgumentException("Unknown synthetic reviewed workflow.");

    private static async Task<(ReviewRequest? Request, IResult? Error)> ReadReviewRequest(HttpRequest request, CancellationToken token)
    {
        if (!request.HasJsonContentType())
            return (null, DemoHost.Error("json_required", 415));
        if (request.ContentLength > DemoHost.MaximumBodyBytes)
            return (null, DemoHost.Error("input_too_large", 413));

        try
        {
            using var memory = new MemoryStream();
            var buffer = new byte[4096];
            int read;
            while ((read = await request.Body.ReadAsync(buffer, token)) > 0)
            {
                if (memory.Length + read > DemoHost.MaximumBodyBytes)
                    return (null, DemoHost.Error("input_too_large", 413));
                memory.Write(buffer, 0, read);
            }

            var json = new UTF8Encoding(false, true).GetString(memory.GetBuffer(), 0, checked((int)memory.Length));
            using var document = JsonDocument.Parse(json.StartsWith('﻿') ? json[1..] : json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return (null, DemoHost.Error("invalid_input", 400));

            var allowed = new HashSet<string>(StringComparer.Ordinal)
            {
                "expectedCaseRevision", "expectedProcessRevision", "expectedAuditRevision",
                "disposition", "reason", "overrideOutcome"
            };
            if (root.EnumerateObject().Any(item => !allowed.Contains(item.Name)))
                return (null, DemoHost.Error("invalid_input", 400));

            var caseRevision = ParseRevision(root, "expectedCaseRevision", 1);
            var processRevision = ParseRevision(root, "expectedProcessRevision", 0);
            var auditRevision = ParseRevision(root, "expectedAuditRevision", 1);
            var disposition = RequiredString(root, "disposition");
            var reason = RequiredString(root, "reason");
            if (reason.Length > 1000
                || (disposition != "ACCEPT_SYSTEM_RESULT" && disposition != "OVERRIDE"))
                return (null, DemoHost.Error("invalid_input", 400));

            AssessmentOutcome? overrideOutcome = null;
            if (root.TryGetProperty("overrideOutcome", out var overrideElement)
                && overrideElement.ValueKind != JsonValueKind.Null)
            {
                if (overrideElement.ValueKind != JsonValueKind.String)
                    return (null, DemoHost.Error("invalid_input", 400));
                overrideOutcome = ParseOutcome(overrideElement.GetString());
                if (overrideOutcome is null)
                    return (null, DemoHost.Error("invalid_input", 400));
            }
            if ((disposition == "OVERRIDE") != (overrideOutcome is not null))
                return (null, DemoHost.Error("invalid_input", 400));

            return (new(caseRevision, processRevision, auditRevision, disposition, reason, overrideOutcome), null);
        }
        catch (Exception exception) when (exception is JsonException or DecoderFallbackException or OverflowException or FormatException)
        {
            return (null, DemoHost.Error("invalid_input", 400));
        }
    }

    private static long ParseRevision(JsonElement root, string name, long minimum)
    {
        var value = RequiredString(root, name);
        if (!long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) || parsed < minimum)
            throw new FormatException();
        return parsed;
    }

    private static string RequiredString(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var element) || element.ValueKind != JsonValueKind.String)
            throw new FormatException();
        var value = element.GetString();
        if (string.IsNullOrWhiteSpace(value)) throw new FormatException();
        return value;
    }

    private static AssessmentOutcome? ParseOutcome(string? value)
        => value switch
        {
            "SUPPORTED" => AssessmentOutcome.Supported,
            "NOT_SUPPORTED" => AssessmentOutcome.NotSupported,
            "INCOMPLETE" => AssessmentOutcome.Incomplete,
            "HUMAN_REVIEW" => AssessmentOutcome.HumanReview,
            "NOT_APPLICABLE" => AssessmentOutcome.NotApplicable,
            _ => null
        };

    private sealed record ReviewRequest(
        long ExpectedCaseRevision,
        long ExpectedProcessRevision,
        long ExpectedAuditRevision,
        string Disposition,
        string Reason,
        AssessmentOutcome? OverrideOutcome);

    internal sealed class SyntheticAuthorizer(SyntheticEntitlementSnapshot entitlement) : ICaseReviewAuthorizer
    {
        public bool Authorize(
            AuthenticatedReviewActor actor,
            CaseReviewState state,
            CaseReviewCommand command,
            CaseReviewPolicy policy)
        {
            if (!PermittedCaseId(state.Process.CaseId.Value)
                || !entitlement.Allows(state.Process.CaseId.Value,
                    command.Disposition == HumanReviewDisposition.AcceptSystemResult ? "ACCEPT" : "OVERRIDE")) return false;
            // This adapter's synthetic entitlement remains explicitly local.
            // The identity/action/case check above uses trusted server configuration.
            // An organizational adapter must load authoritative current grants.
            var grant = new CaseReviewGrant(
                actor,
                state.Process.CaseId, ReviewPolicy,
                [HumanReviewDisposition.AcceptSystemResult, HumanReviewDisposition.Override]);
            return new GrantedCaseReviewAuthorizer([grant])
                .Authorize(actor, state, command, policy);
        }
    }
}
