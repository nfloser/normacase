using System.Globalization;
using System.Text.Json;
using NormaCase.Application.Assessments;
using NormaCase.Application.Intake;
using NormaCase.Application.Outbound;
using NormaCase.Domain.Audit;
using NormaCase.Domain.Cases;
using NormaCase.Domain.Decision;
using NormaCase.Domain.Evidence;

namespace NormaCase.Serialization;

public sealed record OutboundReviewReferenceDocument(string Kind, string Value);

public sealed record ReviewedOutboundResultDocument(
    int FormatVersion,
    string MessageId,
    string CorrelationId,
    string SourceSystemId,
    string UpstreamCaseId,
    string UpstreamMessageId,
    string UpstreamRevision,
    string IntakeAdapterId,
    int IntakeAdapterVersion,
    DateTimeOffset IntakeReceivedAtUtc,
    string CaseId,
    string CaseRevision,
    string CaseTypeId,
    string AssessmentId,
    DateTimeOffset AssessmentRecordedAtUtc,
    string KnowledgePackId,
    string KnowledgeRelease,
    string PlatformVersion,
    string WorkflowId,
    int WorkflowVersion,
    string StateId,
    string ProcessRevision,
    string AuditRevision,
    string ReviewId,
    string ReviewActorId,
    DateTimeOffset ReviewRecordedAtUtc,
    HumanReviewDisposition Disposition,
    string ReviewReason,
    AssessmentOutcome OriginalOutcome,
    AssessmentOutcome HumanOutcome,
    OutboundReviewReferenceDocument? ReviewReference,
    DateOnly AssessmentDate,
    IReadOnlyDictionary<string, CaseValue> Facts,
    IReadOnlyDictionary<string, EvidenceStatus> Evidence,
    IReadOnlyDictionary<string, IReadOnlyList<string>> EvidenceReferences);

public static class ReviewedOutboundResultJson
{
    public static string Serialize(ReviewedOutboundResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        var document = new ReviewedOutboundResultDocument(
            1,
            result.MessageId,
            result.CorrelationId,
            result.Provenance.SourceSystemId,
            result.Provenance.UpstreamCaseId,
            result.Provenance.MessageId,
            Revision(result.Provenance.UpstreamRevision),
            result.Provenance.AdapterId,
            result.Provenance.AdapterVersion,
            result.Provenance.ReceivedAtUtc,
            result.CaseId.Value,
            Revision(result.CaseRevision),
            result.CaseTypeId,
            result.AssessmentId.Value,
            result.AssessmentRecordedAtUtc,
            result.KnowledgePackId,
            result.KnowledgeRelease,
            result.PlatformVersion,
            result.WorkflowId,
            result.WorkflowVersion,
            result.StateId,
            Revision(result.ProcessRevision),
            Revision(result.AuditRevision),
            result.ReviewId.Value,
            result.ReviewActorId,
            result.ReviewRecordedAtUtc,
            result.Disposition,
            result.ReviewReason,
            result.OriginalOutcome,
            result.HumanOutcome,
            result.ReviewReference is null ? null : new(result.ReviewReference.Kind, result.ReviewReference.Value),
            result.Input.AssessmentDate,
            Sorted(result.Input.Facts),
            Sorted(result.Input.Evidence),
            SortedReferences(result.EvidenceReferences));
        return JsonSerializer.Serialize(document, InterchangeJson.Options);
    }

    public static ReviewedOutboundResult Deserialize(string json)
    {
        var document = InterchangeJson.Read<ReviewedOutboundResultDocument>(json);
        if (document.FormatVersion != 1) throw new JsonException("Unsupported outbound result format.");
        try
        {
            var provenance = new IntakeProvenance(
                document.SourceSystemId,
                document.UpstreamCaseId,
                document.UpstreamMessageId,
                ParseRevision(document.UpstreamRevision, positive: true),
                document.IntakeAdapterId,
                document.IntakeAdapterVersion,
                document.IntakeReceivedAtUtc);
            var input = new AssessmentInputSnapshot(document.AssessmentDate, document.Facts, document.Evidence);
            var reference = document.ReviewReference is null
                ? null
                : new ReviewReference(document.ReviewReference.Kind, document.ReviewReference.Value);
            return new ReviewedOutboundResult(
                document.MessageId,
                document.CorrelationId,
                new CaseId(document.CaseId),
                ParseRevision(document.CaseRevision, positive: true),
                document.CaseTypeId,
                new AssessmentId(document.AssessmentId),
                document.AssessmentRecordedAtUtc,
                document.KnowledgePackId,
                document.KnowledgeRelease,
                document.PlatformVersion,
                document.WorkflowId,
                document.WorkflowVersion,
                document.StateId,
                ParseRevision(document.ProcessRevision, positive: false),
                ParseRevision(document.AuditRevision, positive: true),
                new ReviewId(document.ReviewId),
                document.ReviewActorId,
                document.ReviewRecordedAtUtc,
                document.Disposition,
                document.ReviewReason,
                document.OriginalOutcome,
                document.HumanOutcome,
                reference,
                provenance,
                input,
                document.EvidenceReferences);
        }
        catch (Exception exception) when (exception is ArgumentException or OverflowException)
        {
            throw new JsonException("Invalid outbound result document.", exception);
        }
    }

    private static string Revision(long value) => value.ToString(CultureInfo.InvariantCulture);

    private static long ParseRevision(string value, bool positive)
    {
        if (string.IsNullOrEmpty(value)
            || !long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed)
            || parsed.ToString(CultureInfo.InvariantCulture) != value
            || positive && parsed < 1
            || !positive && parsed < 0)
            throw new JsonException("Invalid canonical revision.");
        return parsed;
    }

    private static IReadOnlyDictionary<string,T> Sorted<T>(IReadOnlyDictionary<string,T> source)
        => new SortedDictionary<string,T>(source, StringComparer.Ordinal);

    private static IReadOnlyDictionary<string,IReadOnlyList<string>> SortedReferences(
        IReadOnlyDictionary<string,IReadOnlyList<string>> source)
    {
        var result = new SortedDictionary<string,IReadOnlyList<string>>(StringComparer.Ordinal);
        foreach (var item in source)
            result.Add(item.Key, Array.AsReadOnly(item.Value.Order(StringComparer.Ordinal).ToArray()));
        return result;
    }
}
