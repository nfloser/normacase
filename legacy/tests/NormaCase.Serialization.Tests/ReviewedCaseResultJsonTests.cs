using System.Text.Json;
using NormaCase.Application.Outbound;
using NormaCase.Domain.Audit;
using NormaCase.Domain.Decision;
using NormaCase.Serialization;
using Xunit;
namespace NormaCase.Serialization.Tests;
public sealed class ReviewedCaseResultJsonTests
{
    [Fact]
    public void Roundtrip_preserves_large_revision_strings_and_original_outcome()
    {
        var result=Result();var json=ReviewedCaseResultJson.Serialize(result);
        Assert.Contains("\"caseRevision\":\"9007199254740993\"",json);
        Assert.Equal(result,ReviewedCaseResultJson.Deserialize(json));
        Assert.Equal(json,ReviewedCaseResultJson.Serialize(ReviewedCaseResultJson.Deserialize(json)));
    }
    [Theory]
    [InlineData("\"formatVersion\":1","\"formatVersion\":2")]
    [InlineData("\"formatVersion\":1","\"formatVersion\":1,\"formatVersion\":1")]
    [InlineData("\"formatVersion\":1","\"formatVersion\":1,\"vendorStatus\":true")]
    [InlineData("\"caseRevision\":\"9007199254740993\"","\"caseRevision\":9007199254740993")]
    [InlineData("\"auditRevision\":\"2\"","\"auditRevision\":\"01\"")]
    [InlineData("\"humanOutcome\":\"SUPPORTED\"","\"humanOutcome\":\"UNKNOWN\"")]
    [InlineData("\"messageId\":\"synthetic-message\"","\"messageId\":\"synthetic-message\",\"messageId\":\"substituted\"")]
    [InlineData("\"auditRevision\":\"2\"","\"auditRevision\":\"9223372036854775808\"")]
    [InlineData("\"reviewDisposition\":\"ACCEPT_SYSTEM_RESULT\"","\"reviewDisposition\":1")]
    public void Unsupported_ambiguous_or_coerced_payloads_fail_closed(string from,string to)
        =>Assert.Throws<JsonException>(()=>ReviewedCaseResultJson.Deserialize(ReviewedCaseResultJson.Serialize(Result()).Replace(from,to)));
    [Fact]
    public void Missing_content_and_invalid_outcome_binding_are_rejected()
    {
        Assert.Throws<JsonException>(()=>ReviewedCaseResultJson.Deserialize("{}"));
        Assert.Throws<ArgumentException>(()=>ReviewedCaseResultJson.Serialize(Result() with {HumanOutcome=AssessmentOutcome.NotSupported}));
        Assert.Throws<JsonException>(()=>ReviewedCaseResultJson.Deserialize(ReviewedCaseResultJson.Serialize(Result()).Replace("\"humanOutcome\":\"SUPPORTED\"","\"humanOutcome\":\"NOT_SUPPORTED\"")));
    }
    private static ReviewedCaseResult Result()=>new("synthetic-message","synthetic-correlation","synthetic-source","synthetic-upstream-case","synthetic-upstream-message",7,"synthetic-case",9007199254740993,"synthetic-assessment","synthetic-platform+test","synthetic.demo-a","demo-a-2026.1",new(2026,10,3),"synthetic-workflow",1,"done",1,2,"synthetic-review",new(2026,10,3,13,0,0,TimeSpan.Zero),HumanReviewDisposition.AcceptSystemResult,AssessmentOutcome.Supported,AssessmentOutcome.Supported);
}
