using System.Text.Json;
using NormaCase.Application.Corrections;
using NormaCase.Domain.Audit;
using NormaCase.Domain.Cases;

namespace NormaCase.Serialization;

public sealed record CaseCorrectionLinkDocument(int FormatVersion, string CorrectionId, string CaseId,
    string PreviousAssessmentId, string AssessmentId, long PreviousCaseRevision, long PreviousProcessRevision,
    long PreviousAuditRevision, long CaseRevision, string SourceSystemId, string UpstreamCaseId,
    string PreviousMessageId, string MessageId, string PolicyId, int PolicyVersion,
    string ActorId, string AuthenticationAuthority, DateTimeOffset RecordedAtUtc, string Reason);

public static class CaseCorrectionLinkJson
{
    public static string Serialize(CaseCorrectionLink link)
    {
        Validate(link);
        return JsonSerializer.Serialize(new CaseCorrectionLinkDocument(1, link.CorrectionId, link.CaseId.Value,
            link.PreviousAssessmentId.Value, link.AssessmentId.Value, link.PreviousCaseRevision,
            link.PreviousProcessRevision, link.PreviousAuditRevision, link.CaseRevision, link.SourceSystemId,
            link.UpstreamCaseId, link.PreviousMessageId, link.MessageId, link.PolicyId, link.PolicyVersion,
            link.ActorId, link.AuthenticationAuthority, link.RecordedAtUtc, link.Reason), InterchangeJson.Options);
    }
    public static CaseCorrectionLink Deserialize(string json)
    {
        var d = InterchangeJson.Read<CaseCorrectionLinkDocument>(json);
        if (d.FormatVersion != 1) throw new JsonException("Unsupported correction relation format.");
        try
        {
            var link = new CaseCorrectionLink(d.CorrectionId, new(d.CaseId), new(d.PreviousAssessmentId),
                new(d.AssessmentId), d.PreviousCaseRevision, d.PreviousProcessRevision, d.PreviousAuditRevision,
                d.CaseRevision, d.SourceSystemId, d.UpstreamCaseId, d.PreviousMessageId, d.MessageId,
                d.PolicyId, d.PolicyVersion, d.ActorId, d.AuthenticationAuthority, d.RecordedAtUtc, d.Reason);
            Validate(link); return link;
        }
        catch (ArgumentException) { throw new JsonException("Invalid correction relation."); }
    }
    private static void Validate(CaseCorrectionLink link)
    {
        ArgumentNullException.ThrowIfNull(link);
        if (link.CaseId.IsEmpty || link.PreviousAssessmentId.IsEmpty || link.AssessmentId.IsEmpty
            || link.PreviousAssessmentId == link.AssessmentId || link.PreviousCaseRevision < 1
            || link.PreviousCaseRevision == long.MaxValue || link.CaseRevision != link.PreviousCaseRevision + 1
            || link.PreviousProcessRevision < 0 || link.PreviousAuditRevision < 1 || link.PolicyVersion < 1
            || link.RecordedAtUtc == default || link.RecordedAtUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException("Invalid correction relation binding.");
        foreach (var value in new[] { link.CorrectionId, link.SourceSystemId, link.UpstreamCaseId,
            link.PreviousMessageId, link.MessageId, link.PolicyId }) Text(value, 128);
        Text(link.ActorId, 256); Text(link.AuthenticationAuthority, 256); Text(link.Reason, 1000);
        if (link.PreviousMessageId == link.MessageId) throw new ArgumentException("Correction requires a new message.");
    }
    private static void Text(string value, int maximum)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > maximum || value.Any(char.IsControl))
            throw new ArgumentException("Invalid correction relation text.");
    }
}
