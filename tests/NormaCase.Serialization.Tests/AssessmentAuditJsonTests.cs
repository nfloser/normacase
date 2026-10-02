using System.Text.Json;
using System.Text.Json.Nodes;
using NormaCase.Domain.Audit;
using NormaCase.Domain.Decision;
using NormaCase.Serialization;
using Xunit;

namespace NormaCase.Serialization.Tests;

public sealed class AssessmentAuditJsonTests
{
    [Fact]
    public void Reviews_and_trail_roundtrip_with_explicit_identity_and_metadata()
    {
        var trail = Sample();
        var json = AssessmentAuditJson.Serialize(trail);
        var restored = AssessmentAuditJson.Deserialize(json);
        Assert.Equal(json, AssessmentAuditJson.Serialize(restored));
        Assert.Equal(3, restored.Events.Count);
        Assert.Equal("review-override", restored.Events[2].Review!.ReviewId.Value);
        Assert.Equal("Synthetische Begründung mit ä", restored.Events[2].Review!.Reason);
        Assert.Equal("synthetic-reference", restored.Events[2].Review!.Reference!.Value);
        Assert.Equal(AssessmentOutcome.Incomplete, restored.Events[2].Review!.OverrideOutcome);
        Assert.Null(restored.Events[1].Review!.OverrideOutcome);
        Assert.Equal(1, trail.Events[0].Sequence);
    }

    [Theory]
    [InlineData("version")]
    [InlineData("empty")]
    [InlineData("sequence")]
    [InlineData("assessment")]
    [InlineData("time")]
    [InlineData("actor")]
    [InlineData("kind")]
    [InlineData("creation-review")]
    [InlineData("missing-review")]
    [InlineData("review-id")]
    [InlineData("review-time")]
    [InlineData("review-assessment")]
    [InlineData("review-actor")]
    [InlineData("reason")]
    [InlineData("enum")]
    [InlineData("numeric-enum")]
    [InlineData("override")]
    [InlineData("accept-override")]
    [InlineData("reference")]
    [InlineData("unknown")]
    [InlineData("missing")]
    [InlineData("null")]
    public void Inconsistent_or_ambiguous_history_is_rejected(string mutation)
    {
        var node = JsonNode.Parse(AssessmentAuditJson.Serialize(Sample()))!;
        var events = node["events"]!.AsArray();
        var review = events[2]!["review"]!;
        switch (mutation)
        {
            case "version": node["formatVersion"] = 2; break;
            case "empty": events.Clear(); break;
            case "sequence": events[1]!["sequence"] = 3; break;
            case "assessment": events[1]!["assessmentId"] = "another"; break;
            case "time": events[1]!["occurredAt"] = "2026-10-01T00:00:00+00:00"; break;
            case "actor": events[1]!["actorId"] = "another"; break;
            case "kind": events[1]!["kind"] = "ASSESSMENT_CREATED"; break;
            case "creation-review": events[0]!["review"] = review.DeepClone(); break;
            case "missing-review": events[1]!["review"] = null; break;
            case "review-id": review["reviewId"] = ""; break;
            case "review-time": review["recordedAt"] = "2026-10-03T02:00:00+02:00"; break;
            case "review-assessment": review["assessmentId"] = "another"; break;
            case "review-actor": review["actorId"] = "another"; break;
            case "reason": review["reason"] = " "; break;
            case "enum": review["disposition"] = "UNKNOWN"; break;
            case "numeric-enum": review["disposition"] = 2; break;
            case "override": review["overrideOutcome"] = null; break;
            case "accept-override": events[1]!["review"]!["overrideOutcome"] = "INCOMPLETE"; break;
            case "reference": review["reference"]!["value"] = ""; break;
            case "unknown": review["extra"] = true; break;
            case "missing": review.AsObject().Remove("reason"); break;
            case "null": node["events"] = null; break;
        }
        Assert.Throws<JsonException>(() => AssessmentAuditJson.Deserialize(node.ToJsonString()));
    }

    [Fact]
    public void Duplicate_properties_and_null_events_are_rejected()
    {
        var json = AssessmentAuditJson.Serialize(Sample());
        Assert.Throws<JsonException>(() => AssessmentAuditJson.Deserialize(
            json.Replace("\"formatVersion\":1", "\"formatVersion\":1,\"formatVersion\":1")));
        Assert.Throws<JsonException>(() => AssessmentAuditJson.Deserialize(
            "{\"formatVersion\":1,\"events\":[null]}"));
    }

    private static AssessmentAuditTrail Sample()
    {
        var id = new AssessmentId("assessment-synthetic");
        var time = new DateTimeOffset(2026, 10, 2, 22, 0, 0, TimeSpan.Zero);
        var start = AssessmentAuditTrail.Start(
            AssessmentAuditEvent.AssessmentCreated(1, id, time, "system-synthetic"));
        var accepted = new HumanReviewRecord(new ReviewId("review-accept"), id,
            "actor-synthetic", time.AddMinutes(1), HumanReviewDisposition.AcceptSystemResult,
            "Synthetische Bestätigung");
        var overridden = new HumanReviewRecord(new ReviewId("review-override"), id,
            "actor-synthetic", time.AddMinutes(2), HumanReviewDisposition.Override,
            "Synthetische Begründung mit ä", AssessmentOutcome.Incomplete,
            new ReviewReference("synthetic", "synthetic-reference"));
        return start.Append(AssessmentAuditEvent.HumanReviewRecorded(2, accepted))
            .Append(AssessmentAuditEvent.HumanReviewRecorded(3, overridden));
    }
}
