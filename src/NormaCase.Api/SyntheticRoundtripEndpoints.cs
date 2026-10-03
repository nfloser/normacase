using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Xml;
using Npgsql;
using NormaCase.Application.Assessments;
using NormaCase.Application.Intake;
using NormaCase.Application.Outbound;
using NormaCase.Application.Processing;
using NormaCase.Application.Reviews;
using NormaCase.Application.Triage;
using NormaCase.Domain.Audit;
using NormaCase.Domain.Cases;
using NormaCase.Domain.Workflow;
using NormaCase.Knowledge.Model;
using NormaCase.Persistence.PostgreSql;
using NormaCase.Serialization;
using NormaCase.SyntheticIntegration;
using NormaCase.SyntheticIntegration.Outbound;

namespace NormaCase.Api;

internal static class SyntheticRoundtripEndpoints
{
    internal static void Map(WebApplication app, NpgsqlDataSource source, PostgresCaseReviewStore reviews,
        KnowledgePack pack, string platformVersion)
    {
        var intakeStore = new PostgresNormalizedIntakeStore(source);
        var outbound = new PostgresOutboundDeliveryStore(source);
        var directory = app.Configuration["SyntheticReview:OutboundDirectory"];
        // File delivery is enabled only with an explicit operator-controlled directory.
        var file = string.IsNullOrWhiteSpace(directory) ? null : new BoundedFileReviewedCaseResultSink("synthetic-file", directory);
        var group = app.MapGroup("/api/review").RequireAuthorization();
        group.MapPost("/intake/{format}", async (string format, HttpContext context, CancellationToken token) =>
        {
            if (format is not ("json" or "xml")) return DemoHost.Error("invalid_input", 400);
            if (format == "json" ? !context.Request.HasJsonContentType() : context.Request.ContentType?.Split(';')[0] != "application/xml")
                return DemoHost.Error("invalid_input", 415);
            try
            {
                var text = await ReadBody(context.Request, token);
                var request = format == "json" ? SyntheticIntakeAdapters.Json(text, TimeProvider.System.GetUtcNow()) : SyntheticIntakeAdapters.Xml(text, TimeProvider.System.GetUtcNow());
                // New upstream revisions need a separately approved correction policy.
                if (request.Provenance.UpstreamRevision != 1) return DemoHost.Error("review_forbidden", 403);
                var receipt = await new NormalizedIntakeService(intakeStore).AcceptAsync(request, pack, token);
                var original = receipt.Record;
                var state = await reviews.LoadAsync(original.CaseId, token);
                if (state is null)
                {
                    if (original.KnowledgePackId != pack.Manifest.PackId || original.KnowledgeRelease != pack.Manifest.ReleaseId)
                        return DemoHost.Error("review_conflict", 409);
                    var record = new AssessmentRecorder().Evaluate(pack, original.Input.Facts, original.Input.AssessmentDate, original.Input.Evidence,
                        new(new("assessment-" + original.CaseId.Value), original.CaseId, platformVersion, original.Provenance.ReceivedAtUtc));
                    var process = CaseProcessingInstance.Start(original.CaseId, 1, SyntheticReviewEndpoints.Workflow);
                    var routed = new CaseProcessingRoutingService().Apply(new AssessmentTriageService().Route(record, SyntheticReviewEndpoints.TriagePolicy),
                        process, SyntheticReviewEndpoints.Workflow, SyntheticReviewEndpoints.RoutingPolicy, 1, 0);
                    if (routed.Status != CaseProcessingRoutingStatus.Applied) throw new CaseReviewBindingException();
                    state = await reviews.InitializeRecordedAsync(new(record, 1, routed.Process,
                        AssessmentAuditTrail.Start(AssessmentAuditEvent.AssessmentCreated(1, record.AssessmentId, record.RecordedAtUtc, "synthetic-intake"))), token);
                }
                return Results.Json(new { acceptance = receipt.Acceptance.ToString().ToUpperInvariant(), workCase = SyntheticReviewEndpoints.Detail(state) });
            }
            catch (IntakeConflictException) { return DemoHost.Error("review_conflict", 409); }
            catch (CaseReviewConflictException) { return DemoHost.Error("review_conflict", 409); }
            catch (Exception exception) when (exception is JsonException or XmlException or FormatException or ArgumentException or OverflowException or DecoderFallbackException)
            { return DemoHost.Error("invalid_input", 400); }
        });
        group.MapPost("/work-cases/{caseId}/outbound", async (string caseId, HttpContext context, CancellationToken token) =>
        {
            if (!SyntheticReviewEndpoints.PermittedCaseId(caseId)) return DemoHost.Error("unknown_work_case", 404);
            try
            {
                var request = JsonSerializer.Deserialize<ExportRequest>(await ReadBody(context.Request, token), ExportOptions) ?? throw new JsonException();
                if (!context.Request.HasJsonContentType()) return DemoHost.Error("json_required", 415);
                var state = await reviews.LoadAsync(new(caseId), token);
                if (state is null) return DemoHost.Error("unknown_work_case", 404);
                var intake = await intakeStore.LoadForCaseAsync(new(caseId), token);
                if (intake is null) return DemoHost.Error("review_forbidden", 403);
                var result = new ReviewedCaseResultFactory().Create(intake, state, SyntheticReviewEndpoints.Workflow,
                    new(Revision(request.ExpectedCaseRevision), Revision(request.ExpectedProcessRevision), Revision(request.ExpectedAuditRevision)),
                    request.MessageId, request.CorrelationId);
                IReviewedCaseResultSink sink = request.DestinationId switch
                {
                    "synthetic-inbox" => outbound,
                    "synthetic-file" when file is not null => file,
                    _ => throw new OutboundDeliveryDestinationMismatchException()
                };
                // Message identity is the delivery identity; callers cannot change an independent
                // delivery id to repeat the same reviewed message side effect.
                var receipt = await new ReviewedCaseDeliveryService(outbound).DeliverAsync(new(result.MessageId, sink.DestinationId, result), sink, token);
                return Results.Json(new
                {
                    status = receipt.Status.ToString().ToUpperInvariant(),
                    receipt.IsCommitted,
                    receipt.TransportReference,
                    resultJson = ReviewedCaseResultJson.Serialize(receipt.Result)
                });
            }
            catch (OutboundResultConflictException) { return DemoHost.Error("review_conflict", 409); }
            catch (OutboundDeliveryConflictException) { return DemoHost.Error("review_conflict", 409); }
            catch (Exception exception) when (exception is OutboundResultBindingException or OutboundDeliveryDestinationMismatchException)
            { return DemoHost.Error("review_forbidden", 403); }
            catch (Exception exception) when (exception is JsonException or FormatException or ArgumentException or OverflowException or DecoderFallbackException)
            { return DemoHost.Error("invalid_input", 400); }
            catch (IOException) { return DemoHost.Error("outbound_retryable", 503); }
        });
    }
    private sealed record ExportRequest(string MessageId, string CorrelationId, string DestinationId,
        string ExpectedCaseRevision, string ExpectedProcessRevision, string ExpectedAuditRevision);
    private static readonly JsonSerializerOptions ExportOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow,
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true,
        MaxDepth = 8
    };
    private static long Revision(string text)
    {
        if (!long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var value) || value < 1 || value.ToString(CultureInfo.InvariantCulture) != text) throw new FormatException();
        return value;
    }
    private static async Task<string> ReadBody(HttpRequest request, CancellationToken token)
    {
        if (request.ContentLength > SyntheticIntakeAdapters.MaximumBytes) throw new FormatException();
        using var memory = new MemoryStream();
        var buffer = new byte[4096]; int read;
        while ((read = await request.Body.ReadAsync(buffer, token)) > 0)
        {
            if (memory.Length + read > SyntheticIntakeAdapters.MaximumBytes) throw new FormatException();
            memory.Write(buffer, 0, read);
        }
        var text = new UTF8Encoding(false, true).GetString(memory.GetBuffer(), 0, checked((int)memory.Length));
        if (request.HasJsonContentType())
        {
            using var document = JsonDocument.Parse(text, new JsonDocumentOptions { MaxDepth = 16 });
            RejectDuplicates(document.RootElement);
        }
        return text;
    }
    private static void RejectDuplicates(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject()) { if (!names.Add(property.Name)) throw new JsonException(); RejectDuplicates(property.Value); }
        }
        else if (element.ValueKind == JsonValueKind.Array) foreach (var value in element.EnumerateArray()) RejectDuplicates(value);
    }
}
