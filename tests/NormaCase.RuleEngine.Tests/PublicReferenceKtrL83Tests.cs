using NormaCase.Domain.Cases;
using NormaCase.Domain.Decision;
using NormaCase.Domain.Evidence;
using NormaCase.Knowledge.Serialization;
using NormaCase.RuleEngine.Evaluation;
using Xunit;

namespace NormaCase.RuleEngine.Tests;

public sealed class PublicReferenceKtrL83Tests
{
    private static readonly DateOnly EffectiveDate =
        new(2025, 8, 6);

    private readonly KnowledgePackLoader _loader = new();
    private readonly RuleEvaluator _evaluator = new();

    [Fact]
    public void Pack_is_public_reference_with_pinned_official_source()
    {
        var pack = Load();

        Assert.Equal(
            "public-reference.gba-kt-rl-8-3",
            pack.Manifest.PackId);
        Assert.Equal(
            "kt-rl-8-3-2025-08-06-public-reference.1",
            pack.Manifest.ReleaseId);
        Assert.Equal(
            "PUBLIC_REFERENCE",
            pack.Manifest.ValidationLevel);
        Assert.Equal("IN_REVIEW", pack.Manifest.LifecycleStatus);

        var source = Assert.Single(pack.Sources);
        Assert.Equal(
            "Gemeinsamer Bundesausschuss (G-BA)",
            source.Authority);
        Assert.Equal("2025-05-15", source.Version);
        Assert.Equal(
            new DateOnly(2025, 8, 6),
            source.ValidFrom);
        Assert.Equal(
            new DateOnly(2026, 10, 2),
            source.RetrievedAt);
        Assert.Equal(
            "sha256:114a9ca2dd4ef1b49433898abfffb62570911a58e756c01f5e0f3329c9f93dd6",
            source.ContentHash);
    }

    [Fact]
    public void Qualifying_disability_marker_with_present_card_supports_the_section_8_3_path()
    {
        var facts = CompleteNegativeFacts();
        facts["qualifying_disability_marker_ag_bl_h"] =
            TruthValue.Yes;

        var result = Evaluate(
            facts,
            new()
            {
                ["severe_disability_card"] =
                    EvidenceStatus.Present
            });

        Assert.Equal(AssessmentOutcome.Supported, result.Outcome);
        Assert.Equal(
            "GBA-KT-RL-8-3-EXCEPTION-PATH",
            result.RuleTrace!.RuleId);
        Assert.Equal(
            "GBA-KT-RL-2025-05-15",
            result.RuleTrace.SourceId);
        Assert.Equal(
            "2025-05-15",
            result.RuleTrace.Source.Version);

        var approval = Assert.Single(
            result.DomainOutputs,
            output => output.OutputId == "approval_state");
        Assert.Equal(
            DomainOutputValueKind.Choice,
            approval.Value.Kind);
        Assert.Equal(
            "DEEMED_GRANTED",
            approval.Value.Choice);
    }

    [Theory]
    [InlineData(4)]
    [InlineData(5)]
    public void Pflegegrad_four_or_five_with_present_notice_supports_the_path(
        int careGrade)
    {
        var facts = CompleteNegativeFacts();
        facts["care_grade"] = careGrade;

        var result = Evaluate(
            facts,
            new()
            {
                ["care_grade_notice"] =
                    EvidenceStatus.Present
            });

        Assert.Equal(AssessmentOutcome.Supported, result.Outcome);
    }

    [Fact]
    public void Pflegegrad_three_requires_explicit_permanent_mobility_transport_need()
    {
        var facts = CompleteNegativeFacts();
        facts["care_grade"] = 3;
        facts[
            "care_grade_3_permanent_mobility_transport_need"] =
            TruthValue.Yes;

        var result = Evaluate(
            facts,
            new()
            {
                ["care_grade_notice"] =
                    EvidenceStatus.Present
            });

        Assert.Equal(AssessmentOutcome.Supported, result.Outcome);
    }

    [Theory]
    [InlineData(2, false)]
    [InlineData(3, false)]
    public void Complete_nonqualifying_section_8_3_evidence_is_not_supported(
        int careGrade,
        bool mobilityNeed)
    {
        var facts = CompleteNegativeFacts();
        facts["care_grade"] = careGrade;
        facts[
            "care_grade_3_permanent_mobility_transport_need"] =
            mobilityNeed
                ? TruthValue.Yes
                : TruthValue.No;

        var result = Evaluate(
            facts,
            CompleteEvidence());

        Assert.Equal(
            AssessmentOutcome.NotSupported,
            result.Outcome);
        Assert.Equal(
            ConditionResult.NotMatched,
            result.RuleTrace!.ConditionResult);

        var approval = Assert.Single(
            result.DomainOutputs,
            output => output.OutputId == "approval_state");
        Assert.Equal(
            "NOT_DETERMINED_BY_THIS_PACK",
            approval.Value.Choice);
    }

    [Theory]
    [InlineData(EvidenceStatus.Missing)]
    [InlineData(EvidenceStatus.Conflicting)]
    public void Unavailable_care_grade_notice_requires_human_review(
        EvidenceStatus status)
    {
        var facts = CompleteNegativeFacts();
        facts["care_grade"] = 4;

        var result = Evaluate(
            facts,
            new()
            {
                ["severe_disability_card"] =
                    EvidenceStatus.Present,
                ["care_grade_notice"] = status
            });

        Assert.Equal(
            AssessmentOutcome.HumanReview,
            result.Outcome);
        var approval = Assert.Single(
            result.DomainOutputs,
            output => output.OutputId == "approval_state");
        Assert.Equal(
            DomainOutputValueKind.Unknown,
            approval.Value.Kind);
    }

    [Fact]
    public void Missing_both_branch_evidence_sources_requires_human_review()
    {
        var result = Evaluate(
            CompleteNegativeFacts(),
            new());

        Assert.Equal(
            AssessmentOutcome.HumanReview,
            result.Outcome);
    }

    [Fact]
    public void Missing_required_medical_necessity_remains_incomplete()
    {
        var facts = CompleteNegativeFacts();
        facts.Remove("strict_medical_necessity");
        facts["qualifying_disability_marker_ag_bl_h"] =
            TruthValue.Yes;

        var result = Evaluate(
            facts,
            new()
            {
                ["severe_disability_card"] =
                    EvidenceStatus.Present
            });

        Assert.Equal(
            AssessmentOutcome.Incomplete,
            result.Outcome);
        Assert.Contains(
            "strict_medical_necessity",
            result.MissingRequiredFields);
    }

    [Fact]
    public void Explicit_absence_of_strict_medical_necessity_is_not_supported_for_this_path()
    {
        var facts = CompleteNegativeFacts();
        facts["strict_medical_necessity"] = TruthValue.No;
        facts["qualifying_disability_marker_ag_bl_h"] =
            TruthValue.Yes;

        var result = Evaluate(
            facts,
            new()
            {
                ["severe_disability_card"] =
                    EvidenceStatus.Present
            });

        Assert.Equal(
            AssessmentOutcome.NotSupported,
            result.Outcome);
    }

    [Fact]
    public void Rule_is_not_applied_before_the_pinned_effective_date()
    {
        var result = _evaluator.Evaluate(
            Load(),
            CompleteNegativeFacts(),
            EffectiveDate.AddDays(-1),
            CompleteEvidence());

        Assert.Equal(
            AssessmentOutcome.HumanReview,
            result.Outcome);
        Assert.Null(result.RuleTrace);
    }

    [Fact]
    public void Exact_effective_date_activates_the_reference_rule()
    {
        var facts = CompleteNegativeFacts();
        facts["qualifying_disability_marker_ag_bl_h"] =
            TruthValue.Yes;

        var result = _evaluator.Evaluate(
            Load(),
            facts,
            EffectiveDate,
            new()
            {
                ["severe_disability_card"] =
                    EvidenceStatus.Present
            });

        Assert.Equal(AssessmentOutcome.Supported, result.Outcome);
    }

    private AssessmentResult Evaluate(
        Dictionary<string, CaseValue> facts,
        Dictionary<string, EvidenceStatus> evidence)
        => _evaluator.Evaluate(
            Load(),
            facts,
            new DateOnly(2026, 10, 3),
            evidence);

    private NormaCase.Knowledge.Model.KnowledgePack Load()
        => _loader.LoadFromFile(
            Path.Combine(
                AppContext.BaseDirectory,
                "Fixtures",
                "kt-rl-8-3-pack.json"));

    private static Dictionary<string, CaseValue>
        CompleteNegativeFacts()
        => new()
        {
            ["ambulatory_treatment"] = TruthValue.Yes,
            ["strict_medical_necessity"] = TruthValue.Yes,
            ["qualifying_disability_marker_ag_bl_h"] =
                TruthValue.No,
            ["care_grade"] = 2m,
            ["care_grade_3_permanent_mobility_transport_need"] =
                TruthValue.No
        };

    private static Dictionary<string, EvidenceStatus>
        CompleteEvidence()
        => new()
        {
            ["severe_disability_card"] =
                EvidenceStatus.Present,
            ["care_grade_notice"] =
                EvidenceStatus.Present
        };
}
