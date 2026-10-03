using NormaCase.Application.Assessments;
using NormaCase.Application.Triage;
using NormaCase.Domain.Audit;
using NormaCase.Domain.Cases;
using NormaCase.Domain.Decision;
using NormaCase.Domain.Evidence;
using NormaCase.Knowledge.Serialization;
using NormaCase.RuleEngine.Evaluation;
using Xunit;

namespace NormaCase.Application.Tests;

public sealed class AssessmentTriageServiceTests
{
    private static readonly ApprovalRoutingPolicy Policy = new("synthetic-routing", 1,
        [AssessmentOutcome.Supported, AssessmentOutcome.NotSupported]);

    [Theory]
    [InlineData(true, AssessmentOutcome.Supported)]
    [InlineData(false, AssessmentOutcome.NotSupported)]
    public void Complete_known_results_await_human_approval(bool confirmed, AssessmentOutcome outcome)
    {
        var record = Record(confirmed ? TruthValue.Yes : TruthValue.No);
        var route = new AssessmentTriageService().Route(record, Policy);
        Assert.Equal(outcome, record.Result.Outcome);
        Assert.Equal(CaseProcessingState.ReadyForApproval, route.State);
        Assert.Equal(record.CaseId, route.CaseId);
        Assert.Equal(record.AssessmentId, route.AssessmentId);
        Assert.Equal(Policy.Id, route.PolicyId);
        Assert.Equal(Policy.Version, route.PolicyVersion);
        Assert.Empty(route.Reasons);
    }

    [Fact]
    public void Required_unknown_is_incomplete_and_never_ready()
    {
        var record = Record(TruthValue.Unknown);
        var route = new AssessmentTriageService().Route(record, Policy);
        Assert.Equal(CaseProcessingState.Incomplete, route.State);
        Assert.Contains(route.Reasons, reason => reason.Code == TriageReasonCode.MissingRequiredField);
        Assert.Equal(AssessmentOutcome.Incomplete, record.Result.Outcome);
    }

    [Theory]
    [InlineData(EvidenceStatus.Missing)]
    [InlineData(EvidenceStatus.Conflicting)]
    public void Evidence_uncertainty_requires_human_work(EvidenceStatus status)
    {
        var route = new AssessmentTriageService().Route(Record(TruthValue.Yes, status), Policy);
        Assert.Equal(CaseProcessingState.HumanReview, route.State);
        Assert.Contains(route.Reasons, reason => reason.Code == TriageReasonCode.UnresolvedEvidence);
    }

    [Fact]
    public void Optional_unknown_in_an_unneeded_branch_still_blocks_approval_conservatively()
    {
        var record = Record(TruthValue.Yes);
        var rule = record.Result.RuleTrace!;
        var unknown = rule.Condition with { Result = ConditionResult.Unknown, Children = [] };
        var changed = Copy(record, record.Result with { RuleTrace = rule with
            { Condition = rule.Condition with { Children = [unknown] } } });
        var route = new AssessmentTriageService().Route(changed, Policy);
        Assert.Equal(AssessmentOutcome.Supported, changed.Result.Outcome);
        Assert.Equal(CaseProcessingState.HumanReview, route.State);
        Assert.Contains(route.Reasons, reason => reason.Code == TriageReasonCode.UnknownCondition);
    }

    [Fact]
    public void Unknown_independent_output_blocks_approval_without_changing_platform_result()
    {
        var record = Record(TruthValue.Yes);
        var trace = record.Result.RuleTrace!;
        var changed = Copy(record, record.Result with { DomainOutputs = [new("synthetic-output", 1,
            DomainOutputValue.Unknown, ConditionResult.Unknown,
            trace.Condition with { Result = ConditionResult.Unknown }, trace.Source)] });
        var route = new AssessmentTriageService().Route(changed, Policy);
        Assert.Equal(CaseProcessingState.HumanReview, route.State);
        Assert.Contains(route.Reasons, reason => reason.Code == TriageReasonCode.UnknownOutput
            && reason.Reference == "synthetic-output");
        Assert.Equal(AssessmentOutcome.Supported, changed.Result.Outcome);
    }

    [Fact]
    public void Unknown_derived_value_blocks_approval_even_when_parent_is_known()
    {
        var record = Record(TruthValue.Yes);
        var trace = record.Result.RuleTrace!;
        var expression = new NumericExpressionTrace("sum", CaseValue.FromNumber(1), null, null, null, null,
            [new("field", CaseValue.Unknown, "optional", null, null, null, [])]);
        var changed = Copy(record, record.Result with { RuleTrace = trace with
            { Condition = trace.Condition with { NumericExpression = expression } } });
        var route = new AssessmentTriageService().Route(changed, Policy);
        Assert.Equal(CaseProcessingState.HumanReview, route.State);
        Assert.Contains(route.Reasons, reason => reason.Code == TriageReasonCode.UnknownNumericValue);
    }

    [Fact]
    public void Missing_trace_and_restricted_policy_fail_closed()
    {
        var record = Record(TruthValue.Yes);
        Assert.Equal(CaseProcessingState.HumanReview,
            new AssessmentTriageService().Route(Copy(record, record.Result with { RuleTrace = null }), Policy).State);
        var route = new AssessmentTriageService().Route(record, new("deny-all", 1, []));
        Assert.Equal(CaseProcessingState.HumanReview, route.State);
        Assert.Contains(route.Reasons, reason => reason.Code == TriageReasonCode.PolicyRequiresReview);
    }

    [Fact]
    public void Policy_is_explicit_and_detached_and_cannot_allow_unknown_outcomes()
    {
        var allowed = new List<AssessmentOutcome> { AssessmentOutcome.Supported };
        var policy = new ApprovalRoutingPolicy("test", 1, allowed);
        allowed.Clear();
        Assert.True(policy.Allows(AssessmentOutcome.Supported));
        Assert.Throws<ArgumentException>(() => new ApprovalRoutingPolicy("", 1, []));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ApprovalRoutingPolicy("test", 0, []));
        Assert.Throws<ArgumentException>(() => new ApprovalRoutingPolicy("test", 1, [AssessmentOutcome.Incomplete]));
        Assert.Throws<ArgumentException>(() => new ApprovalRoutingPolicy("test", 1, [AssessmentOutcome.HumanReview]));
        Assert.Throws<ArgumentException>(() => new ApprovalRoutingPolicy("test", 1, [(AssessmentOutcome)99]));
    }

    [Fact]
    public void Routing_is_repeatable_readonly_and_leaves_exact_record_untouched()
    {
        var record = Record(TruthValue.Yes, EvidenceStatus.Missing);
        var original = record.Result;
        var first = new AssessmentTriageService().Route(record, Policy);
        var second = new AssessmentTriageService().Route(record, Policy);
        Assert.Equal(first.State, second.State);
        Assert.Equal(first.Reasons, second.Reasons);
        Assert.Same(original, record.Result);
        Assert.Throws<NotSupportedException>(() => ((IList<TriageReason>)first.Reasons).Clear());
        Assert.Throws<ArgumentNullException>(() => new AssessmentTriageService().Route(null!, Policy));
        Assert.Throws<ArgumentNullException>(() => new AssessmentTriageService().Route(record, null!));
    }

    private static AssessmentRecord Record(TruthValue confirmed, EvidenceStatus evidence = EvidenceStatus.Present)
    {
        var pack = new KnowledgePackLoader().LoadFromFile(Path.Combine(AppContext.BaseDirectory, "Fixtures/demo-c-pack.json"));
        return new AssessmentRecorder().Evaluate(pack,
            new Dictionary<string, CaseValue> { ["request_confirmed"] = confirmed, ["measurement"] = 15m, ["alternative_confirmed"] = TruthValue.No },
            new DateOnly(2026, 10, 2), new Dictionary<string, EvidenceStatus> { ["verification"] = evidence },
            new(new("assessment-synthetic"), new("case-synthetic"), "test-platform",
                new DateTimeOffset(2026, 10, 2, 12, 0, 0, TimeSpan.Zero)));
    }
    private static AssessmentRecord Copy(AssessmentRecord original, AssessmentResult result) => new(
        original.AssessmentId, original.CaseId, original.KnowledgePackId, original.PlatformVersion,
        original.RecordedAtUtc, original.Input, result);
}
