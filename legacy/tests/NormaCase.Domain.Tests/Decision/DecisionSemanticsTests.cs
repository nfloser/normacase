using NormaCase.Domain.Cases;
using NormaCase.Domain.Decision;
using Xunit;

namespace NormaCase.Domain.Tests.Decision;

public sealed class DecisionSemanticsTests
{
    [Fact]
    public void TruthValue_has_a_distinct_unknown_state()
    {
        Assert.NotEqual(TruthValue.Yes, TruthValue.Unknown);
        Assert.NotEqual(TruthValue.No, TruthValue.Unknown);
        Assert.NotEqual(TruthValue.NotApplicable, TruthValue.Unknown);
    }

    [Fact]
    public void AssessmentOutcome_exposes_safe_abstention_states()
    {
        var outcomes = Enum.GetValues<AssessmentOutcome>();

        Assert.Contains(AssessmentOutcome.Incomplete, outcomes);
        Assert.Contains(AssessmentOutcome.HumanReview, outcomes);
    }

    [Fact]
    public void Default_assessment_outcome_is_fail_closed()
    {
        Assert.Equal(AssessmentOutcome.Incomplete, default);
    }

    [Fact]
    public void Default_case_value_is_unknown()
    {
        var value = default(CaseValue);

        Assert.True(value.IsUnknown);
        Assert.Equal(CaseValueKind.Unknown, value.Kind);
    }

    [Fact]
    public void Truth_unknown_maps_to_generic_unknown()
    {
        CaseValue value = TruthValue.Unknown;

        Assert.True(value.IsUnknown);
    }

    [Fact]
    public void Numeric_case_value_preserves_decimal_value()
    {
        CaseValue value = 12.5m;

        Assert.Equal(CaseValueKind.Number, value.Kind);
        Assert.Equal(12.5m, value.Number);
    }
}
