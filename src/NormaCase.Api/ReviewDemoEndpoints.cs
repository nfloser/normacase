using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using NormaCase.Application.Reviews;
using NormaCase.Application.WorkQueues;
using NormaCase.Domain.Audit;
using NormaCase.Domain.Cases;
using NormaCase.Domain.Decision;
using NormaCase.Knowledge.Model;
using NormaCase.Persistence.PostgreSql;
using NormaCase.Serialization;

namespace NormaCase.Api;

internal static class ReviewDemoEndpoints
{
    private sealed record Submission(string AssessmentId, string CaseRevision, string ProcessRevision,
        string AuditRevision, string Disposition, string Reason, string? OverrideOutcome = null);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = false, PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, RespectRequiredConstructorParameters = true,
        RespectNullableAnnotations = true
    };
    internal static void Map(WebApplication app, IReadOnlyDictionary<string, KnowledgePack> packs, string platformVersion)
    {
        var workload = SyntheticWorkload.Create(packs, platformVersion, review: true);
        var repository = app.Services.GetRequiredService<IReviewDemoRepository>();
        app.MapGet("/api/work-queues", async (CancellationToken token) =>
        {
            try
            {
                var items = new List<object>();
                foreach (var seed in workload.Cases)
                {
                    var state = seed.Assessment is null ? null : await repository.GetAsync(seed, workload, token);
                    var process = state?.Process ?? seed.Process;
                    var membership = new CaseWorkQueueProjectionService().Project(process, workload.Queues);
                    items.Add(new { stateId = process.StateId, workflowId = process.WorkflowId, workflowVersion = process.WorkflowVersion, assessmentId = state?.Assessment.AssessmentId.Value, queueId = membership.QueueId ?? throw new CaseReviewBindingException(), caseId = process.CaseId.Value,
                        caseRevision = process.CaseRevision.ToString(CultureInfo.InvariantCulture), processRevision = process.Revision.ToString(CultureInfo.InvariantCulture) });
                }
                var rows = items.Select(item => JsonSerializer.SerializeToElement(item)).ToArray();
                return Results.Json(new { queues = workload.Queues.Queues.Select(queue => queue.QueueId)
                    .Select(id => new { queueId = id, items = rows.Where(item => item.GetProperty("queueId").GetString() == id).ToArray() }) });
            }
            catch (Exception exception) when (exception is CaseReviewStorageException or CaseReviewIntegrityException or PostgresMigrationException)
            { return DemoHost.Error("review_storage", 503); }
        }).RequireAuthorization();
        app.MapGet("/api/work-cases/{caseId}", async (string caseId, CancellationToken token) =>
        {
            var seed = workload.Cases.SingleOrDefault(item => item.Process.CaseId.Value == caseId);
            if (seed is null) return DemoHost.Error("unknown_work_case", 404);
            try { return Results.Json(await Detail(seed, token)); }
            catch (Exception exception) when (exception is CaseReviewStorageException or CaseReviewIntegrityException or PostgresMigrationException)
            { return DemoHost.Error("review_storage", 503); }
        }).RequireAuthorization();
        app.MapPost("/api/work-cases/{caseId}/reviews", (string caseId, HttpRequest request) => DemoHost.HandleJson(request, json =>
        {
            // HandleJson is synchronous after bounded reading; the async storage operation is supplied as an IResult.
            return new ReviewResult(() => Submit(caseId, request.HttpContext.User, json, request.HttpContext.RequestAborted));
        })).RequireAuthorization();

        async Task<object> Detail(SyntheticCaseSeed seed, CancellationToken token)
        {
            var state = seed.Assessment is null ? null : await repository.GetAsync(seed, workload, token);
            var process = state?.Process ?? seed.Process;
            var record = state?.Assessment;
            var membership = new CaseWorkQueueProjectionService().Project(process, workload.Queues);
            return new { caseId = process.CaseId.Value, caseRevision = process.CaseRevision.ToString(CultureInfo.InvariantCulture),
                processRevision = process.Revision.ToString(CultureInfo.InvariantCulture), stateId = process.StateId,
                queueId = membership.QueueId ?? throw new CaseReviewBindingException(), assessmentId = record?.AssessmentId.Value, packId = record?.KnowledgePackId,
                recordedAtUtc = record?.RecordedAtUtc, assessmentJson = record is null ? null : AssessmentJson.Serialize(record.Result, record.PlatformVersion),
                evidence = record?.Input.Evidence.ToDictionary(item => item.Key, item => item.Value.ToString().ToUpperInvariant()) ?? [],
                auditRevision = state?.Audit.Events[^1].Sequence.ToString(CultureInfo.InvariantCulture),
                allowedActions = Actions(process.StateId),
                auditJson = state is null ? null : AssessmentAuditJson.Serialize(state.Audit) };
        }
        async Task<IResult> Submit(string caseId, ClaimsPrincipal principal, string json, CancellationToken token)
        {
            try
            {
                using var document = JsonDocument.Parse(json);
                if (document.RootElement.ValueKind != JsonValueKind.Object || document.RootElement.EnumerateObject().GroupBy(p => p.Name).Any(group => group.Count() > 1))
                    throw new JsonException("Ambiguous submission.");
                var submission = JsonSerializer.Deserialize<Submission>(json, JsonOptions) ?? throw new JsonException("Submission required.");
                var seed = workload.Cases.SingleOrDefault(item => item.Process.CaseId.Value == caseId);
                if (seed?.Assessment is null) return DemoHost.Error("review_denied", 403);
                await repository.GetAsync(seed, workload, token);
                var actor = SyntheticReviewAuthentication.ResolveActor(principal);
                var disposition = submission.Disposition switch
                {
                    "ACCEPT_SYSTEM_RESULT" => HumanReviewDisposition.AcceptSystemResult,
                    "OVERRIDE" => HumanReviewDisposition.Override,
                    _ => throw new JsonException("Unknown disposition.")
                };
                AssessmentOutcome? outcome = submission.OverrideOutcome switch
                {
                    null => null, "SUPPORTED" => AssessmentOutcome.Supported, "NOT_SUPPORTED" => AssessmentOutcome.NotSupported,
                    
                    "NOT_APPLICABLE" => AssessmentOutcome.NotApplicable, _ => throw new JsonException("Unknown outcome.")
                };
                if (submission.Reason.Length > 2000 || submission.AssessmentId.Length > 100)
                    throw new JsonException("Submission exceeds limits.");
                var current = await repository.GetAsync(seed, workload, token);
                var transitions = new Dictionary<HumanReviewDisposition, string>();
                if (current.Process.StateId == "awaiting-approval")
                { transitions[HumanReviewDisposition.AcceptSystemResult] = "accept-result"; transitions[HumanReviewDisposition.Override] = "override-ready"; }
                if (current.Process.StateId == "manual-review") transitions[HumanReviewDisposition.Override] = "override-review";
                var policy = new CaseReviewPolicy("synthetic-local-review-" + current.Process.StateId, 1, workload.Workflow.Id, workload.Workflow.Version, transitions);
                var command = new CaseReviewCommand(new(caseId), new(submission.AssessmentId), new(Guid.NewGuid().ToString("D")),
                    Revision(submission.CaseRevision), Revision(submission.ProcessRevision), Revision(submission.AuditRevision),
                    app.Services.GetRequiredService<TimeProvider>().GetUtcNow(), disposition, submission.Reason, outcome);
                await new CaseReviewService(repository, new Authorizer(workload)).ReviewAsync(actor, command, workload.Workflow, policy, token);
                return Results.Json(await Detail(seed, token));
            }
            catch (CaseReviewConflictException) { return DemoHost.Error("review_conflict", 409); }
            catch (Exception exception) when (exception is CaseReviewDeniedException or CaseReviewPolicyException) { return DemoHost.Error("review_denied", 403); }
            catch (CaseReviewBindingException) { return DemoHost.Error("review_conflict", 409); }
            catch (Exception exception) when (exception is JsonException or ArgumentException or OverflowException) { return DemoHost.Error("invalid_input", 400); }
            catch (Exception exception) when (exception is CaseReviewStorageException or CaseReviewIntegrityException or PostgresMigrationException)
            { return DemoHost.Error("review_storage", 503); }
        }
    }
    private static long Revision(string value) => long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var revision) ? revision : throw new JsonException("Invalid revision.");
    private static string[] Actions(string stateId) => stateId switch
    {
        "awaiting-approval" => ["ACCEPT_SYSTEM_RESULT", "OVERRIDE"], "manual-review" => ["OVERRIDE"], _ => []
    };
    private sealed class Authorizer(SyntheticWorkload workload) : ICaseReviewAuthorizer
    {
        public bool Authorize(AuthenticatedReviewActor actor, CaseReviewState state, CaseReviewCommand command, CaseReviewPolicy policy)
            => actor.ActorId == "synthetic-local:reviewer" && actor.AuthenticationAuthority == "synthetic-local"
                && workload.Cases.Any(seed => seed.Assessment is not null && seed.Process.CaseId == state.Process.CaseId)
                && command.Disposition is HumanReviewDisposition.AcceptSystemResult or HumanReviewDisposition.Override;
    }
    private sealed class ReviewResult(Func<Task<IResult>> operation) : IResult
    {
        public async Task ExecuteAsync(HttpContext context) => await (await operation()).ExecuteAsync(context);
    }
}
