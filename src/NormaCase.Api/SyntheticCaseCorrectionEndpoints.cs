using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Npgsql;
using NormaCase.Application.Corrections;
using NormaCase.Application.Intake;
using NormaCase.Application.Reviews;
using NormaCase.Persistence.PostgreSql;
using NormaCase.Serialization;

namespace NormaCase.Api;

internal static class SyntheticCaseCorrectionEndpoints
{
    private static readonly CaseCorrectionPolicy Information = new("synthetic-information-completion", 1,
        SyntheticReviewEndpoints.Workflow.Id, SyntheticReviewEndpoints.Workflow.Version, ["waiting-information"]);
    private static readonly CaseCorrectionPolicy Manual = new("synthetic-manual-correction", 1,
        SyntheticReviewEndpoints.Workflow.Id, SyntheticReviewEndpoints.Workflow.Version, ["manual-review"]);
    private static readonly CaseClarificationPolicy MissingQuestions = new("synthetic-missing-information", 1,
        SyntheticReviewEndpoints.Workflow.Id, SyntheticReviewEndpoints.Workflow.Version, ["waiting-information"], true);
    private static readonly CaseClarificationPolicy ReviewQuestions = new("synthetic-review-questions", 1,
        SyntheticReviewEndpoints.Workflow.Id, SyntheticReviewEndpoints.Workflow.Version, ["manual-review"], false);
    private static CaseClarificationPolicy? Questions(string stateId) => stateId switch {
        "waiting-information" => MissingQuestions, "manual-review" => ReviewQuestions, _ => null };
    private static CaseCorrectionPolicy? Policy(string stateId) => stateId switch {
        "waiting-information" => Information, "manual-review" => Manual, _ => null };

    internal static void Map(RouteGroupBuilder group, NpgsqlDataSource source, PostgresCaseReviewStore reviews,
        SyntheticLiveEntitlements entitlements, string platformVersion)
    {
        group.MapGet("/work-cases/{caseId}/history", async (string caseId, HttpContext context, CancellationToken token) =>
        {
            if (context.Request.Query.Keys.Any(k => k != "afterVersion")
                || context.Request.Query.Any(x => x.Value.Count != 1)) return DemoHost.Error("invalid_input", 400);
            try
            {
                long? cursor = context.Request.Query.ContainsKey("afterVersion") ? Revision(context.Request.Query["afterVersion"].ToString(), 0) : null;
                return await entitlements.ExecuteAuthorizedAsync<IResult>(SyntheticReviewAuthentication.ResolveActor(context.User), async (grant, locked) =>
                {
                    if (!Visible(caseId, grant)) return DemoHost.Error("unknown_work_case", 404);
                    var page = await reviews.LoadHistoryPageAsync(new(caseId), 25, cursor, locked);
                    return Results.Json(new { entries = page.Take(25).Select(History),
                        nextPageCursor = page.Count > 25 ? page[24].Version.ToString(CultureInfo.InvariantCulture) : null });
                }, token);
            }
            catch (ArgumentException) { return DemoHost.Error("invalid_input", 400); }
        });
        group.MapGet("/work-cases/{caseId}/history/{version}", async (string caseId, string version, HttpContext context, CancellationToken token) =>
        {
            try
            {
                var exact = Revision(version, 0);
                return await entitlements.ExecuteAuthorizedAsync<IResult>(SyntheticReviewAuthentication.ResolveActor(context.User), async (grant, locked) =>
                {
                    if (!Visible(caseId, grant)) return DemoHost.Error("unknown_work_case", 404);
                    var entry = await reviews.LoadRevisionAsync(new(caseId), exact, locked);
                    return entry is null ? DemoHost.Error("unknown_work_case", 404) : Results.Json(History(entry));
                }, token);
            }
            catch (ArgumentException) { return DemoHost.Error("invalid_input", 400); }
        });
        group.MapGet("/work-cases/{caseId}/correction-context", async (string caseId, HttpContext context, CancellationToken token) =>
            await entitlements.ExecuteAuthorizedAsync<IResult>(SyntheticReviewAuthentication.ResolveActor(context.User), async (grant, locked) =>
            {
                if (!Visible(caseId, grant)) return DemoHost.Error("unknown_work_case", 404);
                var current = await reviews.LoadAsync(new(caseId), locked);
                if (current is null) return DemoHost.Error("unknown_work_case", 404);
                var original = await new PostgresNormalizedIntakeStore(source).LoadForCaseAsync(new(caseId), locked);
                var policy = Policy(current.Process.StateId);
                var questions = Questions(current.Process.StateId);
                return Results.Json(new {
                    canClarify = original is not null && questions is not null && grant.Allows(caseId, "CLARIFY"),
                    clarificationPolicyId = questions?.Id, clarificationPolicyVersion = questions?.Version.ToString(CultureInfo.InvariantCulture),
                    clarificationMissingOnly = questions?.RequireMissingTargets ?? true,
                    canCorrect = original is not null && policy is not null && grant.Allows(caseId, "CORRECT"),
                    policyId = policy?.Id, policyVersion = policy?.Version.ToString(CultureInfo.InvariantCulture),
                    normalizedInputJson = original is null ? null : NormalizedIntakeJson.Serialize(original),
                    workCase = SyntheticReviewEndpoints.Detail(current, grant) });
            }, token));
        group.MapGet("/work-cases/{caseId}/clarifications", async (string caseId,HttpContext context,CancellationToken token) =>
        {
            if(context.Request.Query.Keys.Any(k=>k!="afterId")||context.Request.Query.Any(x=>x.Value.Count!=1))
                return DemoHost.Error("invalid_input",400);
            try
            {
                return await entitlements.ExecuteAuthorizedAsync<IResult>(SyntheticReviewAuthentication.ResolveActor(context.User),async(grant,locked)=>{
                    if(!Visible(caseId,grant))return DemoHost.Error("unknown_work_case",404);
                    var current=await reviews.LoadAsync(new(caseId),locked);
                    if(current is null)return DemoHost.Error("unknown_work_case",404);
                    var after=context.Request.Query.ContainsKey("afterId")?context.Request.Query["afterId"].ToString():null;
                    var page=await reviews.ListClarificationsAsync(new(caseId),25,after,locked);
                    return Results.Json(new{entries=page.Take(25).Select(entry=>new{
                        clarificationId=entry.Request.ClarificationId,requestJson=CaseClarificationJson.Serialize(entry.Request),
                        status=entry.ResolvedByCorrectionId is not null?"RESOLVED":entry.Request.CaseRevision<current.Process.CaseRevision?"SUPERSEDED":"OPEN",
                        resolvedByCorrectionId=entry.ResolvedByCorrectionId}),
                        nextPageCursor=page.Count>25?page[24].Request.ClarificationId:null});
                },token);
            }
            catch(ArgumentException){return DemoHost.Error("invalid_input",400);}
        });
        group.MapPost("/work-cases/{caseId}/clarifications",async(string caseId,HttpContext context,CancellationToken token)=>{
            try
            {
                var request=await Body<ClarificationRequest>(context.Request,token);
                var actor=SyntheticReviewAuthentication.ResolveActor(context.User);
                return await entitlements.ExecuteAuthorizedAsync<IResult>(actor,async(grant,locked)=>{
                    if(!Visible(caseId,grant))return DemoHost.Error("unknown_work_case",404);
                    if(!grant.Allows(caseId,"CLARIFY"))return DemoHost.Error("review_forbidden",403);
                    var current=await reviews.LoadAsync(new(caseId),locked);
                    if(current is null)return DemoHost.Error("unknown_work_case",404);
                    if(current.Process.CaseRevision!=Revision(request.ExpectedCaseRevision,1)
                        ||current.Process.Revision!=Revision(request.ExpectedProcessRevision,0)
                        ||current.Audit.Events[^1].Sequence!=Revision(request.ExpectedAuditRevision,1))
                        return DemoHost.Error("review_conflict",409);
                    var policy=Questions(current.Process.StateId);
                    if(policy is null||policy.Id!=request.PolicyId||policy.Version.ToString(CultureInfo.InvariantCulture)!=request.PolicyVersion)
                        return DemoHost.Error("review_forbidden",403);
                    var artifact=await new PostgresKnowledgeReleaseStore(source).LoadAsync(current.Assessment.KnowledgePackId,current.Assessment.Result.KnowledgeRelease,locked)
                        ??throw new KnowledgeReleaseIntegrityException();
                    if(artifact.ValidationLevel!="SYNTHETIC")return DemoHost.Error("review_forbidden",403);
                    var command=new CaseClarificationCommand(request.ClarificationId,Revision(request.ExpectedCaseRevision,1),
                        Revision(request.ExpectedProcessRevision,0),Revision(request.ExpectedAuditRevision,1),
                        TimeProvider.System.GetUtcNow(),request.Reason,request.RequestedFields,request.RequestedEvidence);
                    var record=await reviews.RequestClarificationAsync(new(caseId),state=>new CaseClarificationService(
                        new ClarificationAuthorizer(grant)).Prepare(actor,state,command,policy,artifact.LoadPack(),locked),locked);
                    return Results.Json(new{clarificationId=record.ClarificationId,requestJson=CaseClarificationJson.Serialize(record)});
                },token);
            }
            catch(CaseCorrectionConflictException){return DemoHost.Error("review_conflict",409);}
            catch(Exception exception)when(exception is CaseCorrectionDeniedException or CaseCorrectionPolicyException or CaseCorrectionBindingException)
            {return DemoHost.Error("review_forbidden",403);}
            catch(Exception exception)when(exception is JsonException or ArgumentException or DecoderFallbackException or OverflowException)
            {return DemoHost.Error("invalid_input",400);}
        });
        group.MapPost("/work-cases/{caseId}/corrections", async (string caseId, HttpContext context, CancellationToken token) =>
        {
            try
            {
                var request = await Body<CorrectionRequest>(context.Request, token);
                var actor = SyntheticReviewAuthentication.ResolveActor(context.User);
                return await entitlements.ExecuteAuthorizedAsync<IResult>(actor, async (grant, locked) =>
                {
                    if (!Visible(caseId, grant)) return DemoHost.Error("unknown_work_case", 404);
                    if (!grant.Allows(caseId, "CORRECT")) return DemoHost.Error("review_forbidden", 403);
                    var current = await reviews.LoadAsync(new(caseId), locked);
                    if (current is null) return DemoHost.Error("unknown_work_case", 404);
                    if (current.Process.CaseRevision != Revision(request.ExpectedCaseRevision, 1)
                        || current.Process.Revision != Revision(request.ExpectedProcessRevision, 0)
                        || current.Audit.Events[^1].Sequence != Revision(request.ExpectedAuditRevision, 1))
                        return DemoHost.Error("review_conflict", 409);
                    var policy = Policy(current.Process.StateId);
                    if (policy is null || request.PolicyId != policy.Id || request.PolicyVersion != policy.Version.ToString(CultureInfo.InvariantCulture))
                        return DemoHost.Error("review_forbidden", 403);
                    var original = await new PostgresNormalizedIntakeStore(source).LoadForCaseAsync(new(caseId), locked);
                    if (original is null) return DemoHost.Error("review_forbidden", 403);
                    var retained = await new PostgresKnowledgeReleaseStore(source).LoadAsync(original.KnowledgePackId, original.KnowledgeRelease, locked)
                        ?? throw new KnowledgeReleaseIntegrityException();
                    if (retained.ValidationLevel != "SYNTHETIC") return DemoHost.Error("review_forbidden", 403);
                    var input = CaseInputJson.Deserialize(request.InputJson);
                    var now = TimeProvider.System.GetUtcNow();
                    var corrected = new NormalizedIntakeRequest(original.CaseId, original.CaseTypeId,
                        new(original.Provenance.SourceSystemId, original.Provenance.UpstreamCaseId, request.MessageId,
                            Revision(request.UpstreamRevision, 1), "synthetic-correction", 1, now),
                        input.AssessmentDate, input.Facts, input.Evidence ?? new Dictionary<string, NormaCase.Domain.Evidence.EvidenceStatus>(),
                        (input.Evidence ?? new Dictionary<string, NormaCase.Domain.Evidence.EvidenceStatus>()).ToDictionary(x => x.Key,
                            x => (IReadOnlyList<string>)(x.Value == NormaCase.Domain.Evidence.EvidenceStatus.Present ? new[] { "synthetic-attachment-" + x.Key } : []), StringComparer.Ordinal));
                    var command = new CaseCorrectionCommand(request.CorrectionId,
                        new("correction-assessment-" + Guid.NewGuid().ToString("N")),
                        Revision(request.ExpectedCaseRevision, 1), Revision(request.ExpectedProcessRevision, 0),
                        Revision(request.ExpectedAuditRevision, 1), now, platformVersion, request.Reason, corrected);
                    var result = await new CaseCorrectionService(new CorrectionAuthorizer(grant)).CorrectAsync(reviews, actor, command,
                        retained.LoadPack(), SyntheticReviewEndpoints.Workflow, policy, SyntheticReviewEndpoints.TriagePolicy,
                        SyntheticReviewEndpoints.RoutingPolicy, locked);
                    if (request.ClarificationId is not null)
                        await reviews.ResolveClarificationAsync(request.ClarificationId, result, locked);
                    return Results.Json(new { workCase = SyntheticReviewEndpoints.Detail(result.Next, grant),
                        correctionJson = CaseCorrectionLinkJson.Serialize(result.Link) });
                }, token);
            }
            catch (CaseCorrectionConflictException) { return DemoHost.Error("review_conflict", 409); }
            catch (IntakeConflictException) { return DemoHost.Error("review_conflict", 409); }
            catch (Exception exception) when (exception is CaseCorrectionDeniedException or CaseCorrectionPolicyException or CaseCorrectionBindingException)
            { return DemoHost.Error("review_forbidden", 403); }
            catch (Exception exception) when (exception is JsonException or ArgumentException or DecoderFallbackException or OverflowException)
            { return DemoHost.Error("invalid_input", 400); }
        });
    }
    private static bool Visible(string caseId, SyntheticEntitlementSnapshot grant)
        => SyntheticReviewEndpoints.PermittedCaseId(caseId) && grant.Allows(caseId, "READ");
    private static object History(CaseReviewHistoryEntry entry) => new {
        version = entry.Version.ToString(CultureInfo.InvariantCulture),
        workCase = SyntheticReviewEndpoints.Detail(entry.State, historical: true),
        assessmentRecordJson = AssessmentRecordJson.Serialize(entry.State.Assessment),
        correctionJson = entry.Correction is null ? null : CaseCorrectionLinkJson.Serialize(entry.Correction) };
    private sealed class CorrectionAuthorizer(SyntheticEntitlementSnapshot grant) : ICaseCorrectionAuthorizer
    {
        public bool Authorize(AuthenticatedReviewActor actor, CaseReviewState current, CaseCorrectionCommand command, CaseCorrectionPolicy policy)
            => actor.AuthenticationAuthority == "synthetic-local" && Visible(current.Process.CaseId.Value, grant)
                && grant.Allows(current.Process.CaseId.Value, "CORRECT") && Policy(current.Process.StateId)?.Id == policy.Id;
    }
    private sealed class ClarificationAuthorizer(SyntheticEntitlementSnapshot grant) : ICaseClarificationAuthorizer
    {
        public bool Authorize(AuthenticatedReviewActor actor,CaseReviewState current,CaseClarificationCommand command,CaseClarificationPolicy policy)
            =>actor.AuthenticationAuthority=="synthetic-local"&&Visible(current.Process.CaseId.Value,grant)
                &&grant.Allows(current.Process.CaseId.Value,"CLARIFY")&&Questions(current.Process.StateId)?.Id==policy.Id;
    }
    private sealed record ClarificationRequest(string ClarificationId,string PolicyId,string PolicyVersion,
        string ExpectedCaseRevision,string ExpectedProcessRevision,string ExpectedAuditRevision,string Reason,
        string[] RequestedFields,string[] RequestedEvidence);
    private sealed record CorrectionRequest(string CorrectionId, string MessageId, string UpstreamRevision, string PolicyId, string PolicyVersion,
        string ExpectedCaseRevision, string ExpectedProcessRevision, string ExpectedAuditRevision, string Reason, string InputJson, string? ClarificationId = null);
    private static readonly JsonSerializerOptions Options = new() {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectNullableAnnotations = true, RespectRequiredConstructorParameters = true, MaxDepth = 8 };
    private static async Task<T> Body<T>(HttpRequest request, CancellationToken token) where T:class
    {
        if (!request.HasJsonContentType() || request.ContentLength > 397312) throw new JsonException();
        using var memory = new MemoryStream(); var bytes = new byte[4096]; int read;
        while ((read = await request.Body.ReadAsync(bytes, token)) > 0) {
            if (memory.Length + read > 397312) throw new JsonException();
            memory.Write(bytes, 0, read);
        }
        var text = new UTF8Encoding(false, true).GetString(memory.ToArray());
        using var doc = JsonDocument.Parse(text);
        if (doc.RootElement.ValueKind != JsonValueKind.Object) throw new JsonException();
        var names = doc.RootElement.EnumerateObject().Select(x => x.Name).ToArray();
        if (names.Distinct(StringComparer.Ordinal).Count() != names.Length) throw new JsonException();
        var result = JsonSerializer.Deserialize<T>(text, Options) ?? throw new JsonException();
        if (result is CorrectionRequest correction && new UTF8Encoding(false, true).GetByteCount(correction.InputJson) > 65536) throw new JsonException();
        return result;
    }
    private static long Revision(string value, long minimum)
    {
        if (!long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var revision)
            || revision < minimum || revision.ToString(CultureInfo.InvariantCulture) != value)
            throw new ArgumentException("Explicit canonical revision required.");
        return revision;
    }
}
