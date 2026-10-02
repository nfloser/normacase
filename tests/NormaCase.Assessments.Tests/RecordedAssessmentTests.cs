using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using NormaCase.Assessments;
using NormaCase.Domain.Cases;
using NormaCase.Domain.Decision;
using NormaCase.Knowledge.Serialization;
using NormaCase.RuleEngine.Evaluation;
using NormaCase.Serialization;
using Xunit;

namespace NormaCase.Assessments.Tests;

public sealed class RecordedAssessmentTests
{
    [Fact]
    public void Record_preserves_exact_documents_and_derives_reproducibility_metadata()
    {
        var (input, output) = Documents();
        var recordedAt = new DateTimeOffset(
            2026, 10, 2, 20, 30, 0, TimeSpan.Zero);

        var record = RecordedAssessment.Create(
            "assessment-001",
            "case-001",
            recordedAt,
            input,
            output);

        Assert.Equal("assessment-001", record.AssessmentId);
        Assert.Equal("case-001", record.CaseId);
        Assert.Equal(recordedAt, record.RecordedAtUtc);
        Assert.Equal(new DateOnly(2026, 10, 2), record.AssessmentDate);
        Assert.Equal("test-platform-1", record.PlatformVersion);
        Assert.Equal("demo-a-2026.1", record.KnowledgeRelease);
        Assert.Equal(AssessmentOutcome.Supported, record.SystemOutcome);
        Assert.Equal(input, record.CaseInputJson);
        Assert.Equal(output, record.AssessmentJson);
        Assert.Equal(Sha256(input), record.CaseInputSha256);
        Assert.Equal(Sha256(output), record.AssessmentSha256);
        Assert.True(record.HasValidFingerprints());
    }

    [Fact]
    public void Fingerprint_covers_the_exact_serialized_bytes()
    {
        var (input, output) = Documents();

        var first = RecordedAssessment.Create(
            "assessment-001",
            "case-001",
            Utc(),
            input,
            output);
        var second = RecordedAssessment.Create(
            "assessment-002",
            "case-001",
            Utc(),
            input + Environment.NewLine,
            output);

        Assert.NotEqual(
            first.CaseInputSha256,
            second.CaseInputSha256);
        Assert.Equal(
            first.AssessmentSha256,
            second.AssessmentSha256);
    }

    [Fact]
    public void Restore_rejects_replaced_documents_and_inconsistent_metadata()
    {
        var (input, output) = Documents();
        var original = RecordedAssessment.Create(
            "assessment-001",
            "case-001",
            Utc(),
            input,
            output);

        Assert.Throws<InvalidDataException>(
            () => RecordedAssessment.Restore(
                original.AssessmentId,
                original.CaseId,
                original.RecordedAtUtc,
                original.AssessmentDate,
                original.PlatformVersion,
                original.KnowledgeRelease,
                original.SystemOutcome,
                original.CaseInputJson + Environment.NewLine,
                original.AssessmentJson,
                original.CaseInputSha256,
                original.AssessmentSha256));

        Assert.Throws<InvalidDataException>(
            () => RecordedAssessment.Restore(
                original.AssessmentId,
                original.CaseId,
                original.RecordedAtUtc,
                original.AssessmentDate,
                "different-platform",
                original.KnowledgeRelease,
                original.SystemOutcome,
                original.CaseInputJson,
                original.AssessmentJson,
                original.CaseInputSha256,
                original.AssessmentSha256));
    }

    [Fact]
    public void Restore_roundtrips_an_untampered_record()
    {
        var (input, output) = Documents();
        var original = RecordedAssessment.Create(
            "assessment-001",
            "case-001",
            Utc(),
            input,
            output);

        var restored = RecordedAssessment.Restore(
            original.AssessmentId,
            original.CaseId,
            original.RecordedAtUtc,
            original.AssessmentDate,
            original.PlatformVersion,
            original.KnowledgeRelease,
            original.SystemOutcome,
            original.CaseInputJson,
            original.AssessmentJson,
            original.CaseInputSha256,
            original.AssessmentSha256);

        Assert.Equal(original.AssessmentId, restored.AssessmentId);
        Assert.Equal(original.CaseInputSha256, restored.CaseInputSha256);
        Assert.Equal(original.AssessmentSha256, restored.AssessmentSha256);
        Assert.True(restored.HasValidFingerprints());
    }

    [Fact]
    public void Assessment_and_input_dates_must_match()
    {
        var (input, output) = Documents();
        var node = JsonNode.Parse(output)!;
        node["assessment"]!["assessmentDate"] = "2026-10-03";

        Assert.Throws<InvalidDataException>(
            () => RecordedAssessment.Create(
                "assessment-001",
                "case-001",
                Utc(),
                input,
                node.ToJsonString()));
    }

    [Fact]
    public void Malformed_documents_fail_closed()
    {
        var (input, output) = Documents();

        Assert.ThrowsAny<Exception>(
            () => RecordedAssessment.Create(
                "assessment-001",
                "case-001",
                Utc(),
                "{}",
                output));

        Assert.ThrowsAny<Exception>(
            () => RecordedAssessment.Create(
                "assessment-001",
                "case-001",
                Utc(),
                input,
                "{}"));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("contains whitespace")]
    [InlineData("../path")]
    [InlineData("ä")]
    public void Technical_identifiers_are_restricted(string id)
    {
        var (input, output) = Documents();

        Assert.Throws<ArgumentException>(
            () => RecordedAssessment.Create(
                id,
                "case-001",
                Utc(),
                input,
                output));

        Assert.Throws<ArgumentException>(
            () => RecordedAssessment.Create(
                "assessment-001",
                id,
                Utc(),
                input,
                output));
    }

    [Fact]
    public void Recorded_at_must_be_an_explicit_UTC_instant()
    {
        var (input, output) = Documents();

        Assert.Throws<ArgumentException>(
            () => RecordedAssessment.Create(
                "assessment-001",
                "case-001",
                new DateTimeOffset(
                    2026, 10, 2, 22, 30, 0,
                    TimeSpan.FromHours(2)),
                input,
                output));
    }

    [Fact]
    public void Human_override_is_a_separate_provenance_record()
    {
        var (input, output) = Documents();
        var assessment = RecordedAssessment.Create(
            "assessment-001",
            "case-001",
            Utc(),
            input,
            output);

        var review = AssessmentReview.CreateOverride(
            "review-001",
            assessment,
            "reviewer-17",
            new DateTimeOffset(
                2026, 10, 3, 8, 15, 0,
                TimeSpan.Zero),
            AssessmentOutcome.HumanReview,
            "Synthetischer Review-Grund.");

        Assert.Equal("review-001", review.ReviewId);
        Assert.Equal(assessment.AssessmentId, review.AssessmentId);
        Assert.Equal("reviewer-17", review.ActorId);
        Assert.Equal(
            AssessmentReviewAction.OverrideSystemOutcome,
            review.Action);
        Assert.Equal(
            AssessmentOutcome.Supported,
            review.OriginalOutcome);
        Assert.Equal(
            AssessmentOutcome.HumanReview,
            review.OverrideOutcome);
        Assert.Equal(
            "Synthetischer Review-Grund.",
            review.Reason);

        Assert.Equal(
            AssessmentOutcome.Supported,
            assessment.SystemOutcome);
        Assert.True(assessment.HasValidFingerprints());
    }

    [Fact]
    public void Confirmation_preserves_original_outcome_without_an_override()
    {
        var (input, output) = Documents();
        var assessment = RecordedAssessment.Create(
            "assessment-001",
            "case-001",
            Utc(),
            input,
            output);

        var review = AssessmentReview.CreateConfirmation(
            "review-001",
            assessment,
            "reviewer-17",
            Utc(),
            "Synthetisch geprüft.");

        Assert.Equal(
            AssessmentReviewAction.ConfirmSystemOutcome,
            review.Action);
        Assert.Equal(
            assessment.SystemOutcome,
            review.OriginalOutcome);
        Assert.Null(review.OverrideOutcome);
    }

    [Fact]
    public void Review_rejects_non_UTC_time_and_empty_reason()
    {
        var (input, output) = Documents();
        var assessment = RecordedAssessment.Create(
            "assessment-001",
            "case-001",
            Utc(),
            input,
            output);

        Assert.Throws<ArgumentException>(
            () => AssessmentReview.CreateConfirmation(
                "review-001",
                assessment,
                "reviewer-17",
                new DateTimeOffset(
                    2026, 10, 2, 22, 30, 0,
                    TimeSpan.FromHours(2)),
                "Grund"));

        Assert.Throws<ArgumentException>(
            () => AssessmentReview.CreateConfirmation(
                "review-001",
                assessment,
                "reviewer-17",
                Utc(),
                " "));
    }

    private static (
        string Input,
        string Output) Documents()
    {
        const string input = """
            {
              "formatVersion": 1,
              "assessmentDate": "2026-10-02",
              "facts": {
                "criterion_a": {
                  "kind": "TRUTH",
                  "truth": "YES"
                },
                "criterion_b": {
                  "kind": "TRUTH",
                  "truth": "YES"
                },
                "criterion_c": {
                  "kind": "TRUTH",
                  "truth": "NO"
                }
              }
            }
            """;

        var pack = new KnowledgePackLoader().LoadFromFile(
            Path.Combine(
                AppContext.BaseDirectory,
                "Fixtures",
                "demo-a-pack.json"));
        var parsed = CaseInputJson.Deserialize(input);
        var result = new RuleEvaluator().Evaluate(
            pack,
            parsed.Facts,
            parsed.AssessmentDate,
            parsed.Evidence);

        return (
            input,
            AssessmentJson.Serialize(
                result,
                "test-platform-1"));
    }

    private static DateTimeOffset Utc()
        => new(
            2026, 10, 2, 20, 30, 0,
            TimeSpan.Zero);

    private static string Sha256(string value)
        => Convert.ToHexString(
            SHA256.HashData(
                Encoding.UTF8.GetBytes(value)));
}
