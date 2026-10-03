using System.Text.Json;
using NormaCase.Application.Assessments;
using NormaCase.Application.Intake;
using NormaCase.Application.Outbound;
using NormaCase.Domain.Audit;
using NormaCase.Domain.Cases;
using NormaCase.Domain.Decision;
using NormaCase.Domain.Evidence;
using NormaCase.Serialization;
using Xunit;

namespace NormaCase.Serialization.Tests;

public sealed class ReviewedOutboundResultJsonTests
{
    [Fact]
    public void Roundtrip_is_exact_and_keeps_large_revisions_as_strings()
    {
        var result=Result();
        var json=ReviewedOutboundResultJson.Serialize(result);

        Assert.Contains("\"upstreamRevision\":\"9223372036854775794\"",json,StringComparison.Ordinal);
        Assert.Contains("\"caseRevision\":\"9223372036854775795\"",json,StringComparison.Ordinal);
        Assert.Contains("\"processRevision\":\"9223372036854775796\"",json,StringComparison.Ordinal);
        Assert.Contains("\"auditRevision\":\"9223372036854775797\"",json,StringComparison.Ordinal);

        var restored=ReviewedOutboundResultJson.Deserialize(json);
        Assert.Equal(json,ReviewedOutboundResultJson.Serialize(restored));
        Assert.Equal(123456789.1234567890123456789m,restored.Input.Facts["measurement"].Number);
        Assert.True(restored.Input.Facts["unknown"].IsUnknown);
        Assert.Equal(AssessmentOutcome.NotSupported,restored.OriginalOutcome);
        Assert.Equal(AssessmentOutcome.Supported,restored.HumanOutcome);
        Assert.Equal("synthetic-upstream",restored.Provenance.SourceSystemId);
        Assert.Equal("ticket",restored.ReviewReference!.Kind);
    }

    [Theory]
    [InlineData("\"formatVersion\":1", "\"formatVersion\":2")]
    [InlineData("\"formatVersion\":1", "\"formatVersion\":1,\"formatVersion\":1")]
    [InlineData("\"formatVersion\":1", "\"formatVersion\":1,\"unexpected\":true")]
    [InlineData("\"caseRevision\":\"9223372036854775795\"", "\"caseRevision\":\"01\"")]
    [InlineData("\"caseRevision\":\"9223372036854775795\"", "\"caseRevision\":\"9e2\"")]
    [InlineData("\"caseRevision\":\"9223372036854775795\"", "\"caseRevision\":9223372036854775795")]
    [InlineData("\"processRevision\":\"9223372036854775796\"", "\"processRevision\":\"-1\"")]
    public void Unsupported_ambiguous_or_noncanonical_documents_fail_closed(string from,string to)
    {
        var json=ReviewedOutboundResultJson.Serialize(Result()).Replace(from,to,StringComparison.Ordinal);
        Assert.Throws<JsonException>(()=>ReviewedOutboundResultJson.Deserialize(json));
    }

    [Fact]
    public void Missing_required_properties_and_invalid_accepted_outcome_are_rejected()
    {
        Assert.Throws<JsonException>(()=>ReviewedOutboundResultJson.Deserialize("{}"));

        var accepted=Result(HumanReviewDisposition.AcceptSystemResult,AssessmentOutcome.NotSupported);
        var json=ReviewedOutboundResultJson.Serialize(accepted)
            .Replace("\"humanOutcome\":\"NOT_SUPPORTED\"","\"humanOutcome\":\"SUPPORTED\"",StringComparison.Ordinal);
        Assert.Throws<JsonException>(()=>ReviewedOutboundResultJson.Deserialize(json));
    }

    private static ReviewedOutboundResult Result(
        HumanReviewDisposition disposition=HumanReviewDisposition.Override,
        AssessmentOutcome humanOutcome=AssessmentOutcome.Supported)
    {
        var facts=new Dictionary<string,CaseValue>
        {
            ["measurement"]=123456789.1234567890123456789m,
            ["unknown"]=CaseValue.Unknown
        };
        var evidence=new Dictionary<string,EvidenceStatus>{{"verification",EvidenceStatus.Present}};
        var input=new AssessmentInputSnapshot(new(2026,10,3),facts,evidence);
        var provenance=new IntakeProvenance("synthetic-upstream","case-42","message-9",
            long.MaxValue-13,"synthetic-adapter",4,new(2026,10,3,12,0,0,TimeSpan.Zero));
        return new(
            "outbound-message-1","correlation-1",new("case-outbound"),long.MaxValue-12,"synthetic-type",
            new("assessment-outbound"),new(2026,10,3,12,5,0,TimeSpan.Zero),
            "synthetic.pack","release-2026-10","platform-1.4.0","synthetic-workflow",7,"reviewed",
            long.MaxValue-11,long.MaxValue-10,new("review-9"),"synthetic-local:reviewer",
            new(2026,10,3,12,10,0,TimeSpan.Zero),disposition,"Synthetic reason",
            AssessmentOutcome.NotSupported,humanOutcome,new("ticket","synthetic-42"),
            provenance,input,new Dictionary<string,IReadOnlyList<string>>
            {
                ["verification"]=new[]{"doc-b","doc-a"}
            });
    }
}
