using System.Globalization;
using System.Text;
using System.Text.Json;
using NormaCase.Application.Reviews;
using NormaCase.Domain.Audit;
using NormaCase.Domain.Cases;
using NormaCase.Domain.Decision;
using NormaCase.Persistence.PostgreSql;

namespace NormaCase.Api;

internal static class SyntheticBatchReviewEndpoints
{
    private static readonly BatchReviewPolicy Policy = new(
        "synthetic-reviewed-batch-policy", 1,
        SyntheticReviewEndpoints.ReviewPolicy.Id,
        SyntheticReviewEndpoints.ReviewPolicy.Version,
        BatchReviewPolicy.AbsoluteMaximumCommands,
        BatchReviewFailureMode.Continue);

    internal static void Map(RouteGroupBuilder group, PostgresCaseReviewStore store,
        SyntheticReviewCredential credential)
    {
        group.MapPost("/batch-reviews", async (HttpContext context, CancellationToken token) =>
        {
            var parsed = await ReadRequest(context.Request, token);
            if (parsed.Error is not null) return parsed.Error;
            var request = parsed.Request!;

            AuthenticatedReviewActor actor;
            try { actor = SyntheticReviewAuthentication.ResolveActor(context.User); }
            catch (InvalidOperationException) { return DemoHost.Error("review_forbidden", 403); }

            if (request.Items.Any(item => !SyntheticReviewEndpoints.PermittedCaseId(item.CaseId)
                    || !credential.Allows(actor, item.CaseId, "READ")))
                return DemoHost.Error("unknown_work_case", 404);
            if (request.Items.Any(item => !credential.Allows(actor, item.CaseId, "BATCH")))
                return DemoHost.Error("review_forbidden", 403);

            // Resolve every caller-visible aggregate before the first independent mutation.
            // This avoids a partial batch when one requested case/assessment is unknown.
            foreach (var item in request.Items)
            {
                var current = await store.LoadAsync(new CaseId(item.CaseId), token);
                if (current is null || current.Assessment.AssessmentId.Value != item.AssessmentId)
                    return DemoHost.Error("unknown_work_case", 404);
            }

            var recordedAt = TimeProvider.System.GetUtcNow();
            var commands = request.Items.Select(item => new CaseReviewCommand(
                new(item.CaseId), new(item.AssessmentId), new(item.ReviewId),
                item.ExpectedCaseRevision, item.ExpectedProcessRevision, item.ExpectedAuditRevision,
                recordedAt,
                item.Disposition == "ACCEPT_SYSTEM_RESULT"
                    ? HumanReviewDisposition.AcceptSystemResult
                    : HumanReviewDisposition.Override,
                item.Reason, item.OverrideOutcome)).ToArray();

            try
            {
                var service = new BatchReviewService(new CaseReviewService(
                    store, new SyntheticReviewEndpoints.SyntheticAuthorizer(credential)));
                var result = await service.ReviewAsync(actor, commands,
                    SyntheticReviewEndpoints.Workflow, SyntheticReviewEndpoints.ReviewPolicy, Policy, token);
                return Results.Json(new
                {
                    policyId = result.PolicyId,
                    policyVersion = result.PolicyVersion.ToString(CultureInfo.InvariantCulture),
                    items = result.Items.Select(item => new
                    {
                        caseId = item.CaseId.Value,
                        reviewId = item.ReviewId.Value,
                        status = Status(item.Status)
                    }).ToArray()
                });
            }
            catch (Exception exception) when (exception is ArgumentException or OverflowException)
            {
                return DemoHost.Error("invalid_input", 400);
            }
        });
    }

    private static async Task<(BatchRequest? Request, IResult? Error)> ReadRequest(
        HttpRequest request, CancellationToken token)
    {
        if (!request.HasJsonContentType()) return (null, DemoHost.Error("json_required", 415));
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
            var json = new UTF8Encoding(false, true).GetString(
                memory.GetBuffer(), 0, checked((int)memory.Length));
            using var document = JsonDocument.Parse(json.StartsWith('\ufeff') ? json[1..] : json);
            var root = document.RootElement;
            if (!IsObject(root, ["policyId", "policyVersion", "items"])) throw new FormatException();
            if (RequiredString(root, "policyId", 128) != Policy.Id
                || ParsePositiveInt(root, "policyVersion") != Policy.Version
                || !root.TryGetProperty("items", out var itemsElement)
                || itemsElement.ValueKind != JsonValueKind.Array
                || itemsElement.GetArrayLength() is < 1 or > BatchReviewPolicy.AbsoluteMaximumCommands)
                throw new FormatException();

            var items = new List<BatchItem>(itemsElement.GetArrayLength());
            foreach (var element in itemsElement.EnumerateArray()) items.Add(ParseItem(element));
            if (items.Select(item => item.CaseId).Distinct(StringComparer.Ordinal).Count() != items.Count
                || items.Select(item => item.ReviewId).Distinct(StringComparer.Ordinal).Count() != items.Count)
                throw new FormatException();
            return (new(items.ToArray()), null);
        }
        catch (Exception exception) when (exception is JsonException or DecoderFallbackException
            or OverflowException or FormatException or ArgumentException)
        {
            return (null, DemoHost.Error("invalid_input", 400));
        }
    }

    private static BatchItem ParseItem(JsonElement element)
    {
        if (!IsObject(element,
            ["caseId", "assessmentId", "reviewId", "expectedCaseRevision",
             "expectedProcessRevision", "expectedAuditRevision", "disposition", "reason", "overrideOutcome"]))
            throw new FormatException();
        var disposition = RequiredString(element, "disposition", 32);
        if (disposition is not ("ACCEPT_SYSTEM_RESULT" or "OVERRIDE")) throw new FormatException();
        AssessmentOutcome? outcome = null;
        if (element.TryGetProperty("overrideOutcome", out var outcomeElement)
            && outcomeElement.ValueKind != JsonValueKind.Null)
        {
            if (outcomeElement.ValueKind != JsonValueKind.String) throw new FormatException();
            outcome = outcomeElement.GetString() switch
            {
                "SUPPORTED" => AssessmentOutcome.Supported,
                "NOT_SUPPORTED" => AssessmentOutcome.NotSupported,
                "INCOMPLETE" => AssessmentOutcome.Incomplete,
                "HUMAN_REVIEW" => AssessmentOutcome.HumanReview,
                "NOT_APPLICABLE" => AssessmentOutcome.NotApplicable,
                _ => throw new FormatException()
            };
        }
        if ((disposition == "OVERRIDE") != (outcome is not null)) throw new FormatException();
        return new(
            RequiredString(element, "caseId", 128),
            RequiredString(element, "assessmentId", 128),
            RequiredString(element, "reviewId", 128),
            ParseRevision(element, "expectedCaseRevision", 1),
            ParseRevision(element, "expectedProcessRevision", 0),
            ParseRevision(element, "expectedAuditRevision", 1),
            disposition,
            RequiredString(element, "reason", 1000),
            outcome);
    }

    private static bool IsObject(JsonElement element, string[] allowed)
    {
        if (element.ValueKind != JsonValueKind.Object) return false;
        var properties = element.EnumerateObject().ToArray();
        return properties.All(property => allowed.Contains(property.Name, StringComparer.Ordinal))
            && properties.Select(property => property.Name).Distinct(StringComparer.Ordinal).Count() == properties.Length;
    }

    private static string RequiredString(JsonElement element, string name, int maximumLength)
    {
        if (!element.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String)
            throw new FormatException();
        var text = value.GetString();
        if (string.IsNullOrWhiteSpace(text) || text.Length > maximumLength) throw new FormatException();
        return text;
    }

    private static int ParsePositiveInt(JsonElement element, string name)
    {
        var value = RequiredString(element, name, 10);
        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) || parsed < 1)
            throw new FormatException();
        return parsed;
    }

    private static long ParseRevision(JsonElement element, string name, long minimum)
    {
        var value = RequiredString(element, name, 20);
        if (!long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) || parsed < minimum)
            throw new FormatException();
        return parsed;
    }

    private static string Status(BatchReviewItemStatus status) => status switch
    {
        BatchReviewItemStatus.Committed => "COMMITTED",
        BatchReviewItemStatus.Denied => "DENIED",
        BatchReviewItemStatus.Conflict => "CONFLICT",
        BatchReviewItemStatus.PolicyRejected => "POLICY_REJECTED",
        BatchReviewItemStatus.NotAttempted => "NOT_ATTEMPTED",
        _ => throw new ArgumentOutOfRangeException(nameof(status))
    };

    private sealed record BatchRequest(IReadOnlyList<BatchItem> Items);
    private sealed record BatchItem(
        string CaseId, string AssessmentId, string ReviewId,
        long ExpectedCaseRevision, long ExpectedProcessRevision, long ExpectedAuditRevision,
        string Disposition, string Reason, AssessmentOutcome? OverrideOutcome);
}
