using NormaCase.Domain.Decision;

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
}
