using NormaCase.Domain.Cases;
using NormaCase.Domain.Decision;
using NormaCase.Knowledge.Serialization;
using NormaCase.RuleEngine.Evaluation;
using Xunit;

namespace NormaCase.RuleEngine.Tests;

public sealed class PublicReferencePflegeAdultScoreTests
{
    private static readonly DateOnly EffectiveDate =
        new(2026, 10, 1);

    private readonly KnowledgePackLoader _loader = new();
    private readonly RuleEvaluator _evaluator = new();

    [Fact]
    public void Pack_is_public_reference_with_pinned_md_bund_source()
    {
        var pack = Load();

        Assert.Equal(
            "public-reference.md-bund-pflege-adult-score",
            pack.Manifest.PackId);
        Assert.Equal(
            "pflege-adult-score-2026-10-01-public-reference.1",
            pack.Manifest.ReleaseId);
        Assert.Equal("PUBLIC_REFERENCE", pack.Manifest.ValidationLevel);
        Assert.Equal("IN_REVIEW", pack.Manifest.LifecycleStatus);

        var source = Assert.Single(pack.Sources);
        Assert.Equal("Medizinischer Dienst Bund (KöR)", source.Authority);
        Assert.Equal("2026-08-26", source.Version);
        Assert.Equal(EffectiveDate, source.ValidFrom);
        Assert.Equal(new DateOnly(2026, 10, 2), source.RetrievedAt);
        Assert.Equal(
            "sha256:f39b25b55a30cd2dcf9b5ac1547f73be3f9d077b12c87f69ded38e6fac104e4b",
            source.ContentHash);
    }

    [Theory]
    [MemberData(nameof(ModuleBandCases))]
    public void Every_published_module_band_boundary_maps_to_its_weighted_value(
        int module,
        decimal rawSum,
        decimal expectedWeighted)
    {
        var facts = ZeroFacts();
        facts[FieldFor(module)] = rawSum;

        var result = Evaluate(facts);
        var expression = Assert.IsType<NumericExpressionTrace>(
            result.RuleTrace!.Condition.NumericExpression);

        Assert.Equal(
            expectedWeighted,
            WeightedValue(expression, module));
    }

    [Fact]
    public void Modules_two_and_three_contribute_only_the_higher_weighted_value()
    {
        var facts = ZeroFacts();
        facts["module_2_sum"] = 17m;
        facts["module_3_sum"] = 7m;

        var result = Evaluate(facts);
        var expression = result.RuleTrace!.Condition.NumericExpression!;

        Assert.Equal(15m, expression.Value.Number);
        Assert.Equal("max", expression.Children[1].Kind);
        Assert.Equal(15m, expression.Children[1].Children[0].Value.Number);
        Assert.Equal(15m, expression.Children[1].Children[1].Value.Number);
        Assert.Equal(
            "NOT_REACHED",
            Threshold(result, "score_threshold_27").Value.Choice);
    }

    [Theory]
    [MemberData(nameof(TotalScoreCases))]
    public void Total_score_thresholds_follow_the_published_boundaries(
        decimal module1,
        decimal module2,
        decimal module4,
        decimal module5,
        decimal module6,
        decimal expectedTotal,
        string threshold12_5,
        string threshold27,
        string threshold47_5,
        string threshold70,
        string threshold90)
    {
        var facts = ZeroFacts();
        facts["module_1_sum"] = module1;
        facts["module_2_sum"] = module2;
        facts["module_4_sum"] = module4;
        facts["module_5_sum"] = module5;
        facts["module_6_sum"] = module6;

        var result = Evaluate(facts);

        Assert.Equal(
            expectedTotal,
            result.RuleTrace!.Condition.Actual!.Value.Number);
        Assert.Equal(
            threshold12_5,
            Threshold(result, "score_threshold_12_5").Value.Choice);
        Assert.Equal(
            threshold27,
            Threshold(result, "score_threshold_27").Value.Choice);
        Assert.Equal(
            threshold47_5,
            Threshold(result, "score_threshold_47_5").Value.Choice);
        Assert.Equal(
            threshold70,
            Threshold(result, "score_threshold_70").Value.Choice);
        Assert.Equal(
            threshold90,
            Threshold(result, "score_threshold_90").Value.Choice);
    }

    [Theory]
    [InlineData(1, 16)]
    [InlineData(2, 34)]
    [InlineData(3, 66)]
    [InlineData(4, 55)]
    [InlineData(5, 16)]
    [InlineData(6, 19)]
    [InlineData(1, -1)]
    public void Out_of_table_module_sum_fails_closed(
        int module,
        int invalidRawSum)
    {
        var facts = ZeroFacts();
        facts[FieldFor(module)] = invalidRawSum;

        var result = Evaluate(facts);

        Assert.Equal(AssessmentOutcome.HumanReview, result.Outcome);
        Assert.Equal(
            ConditionResult.Unknown,
            result.RuleTrace!.ConditionResult);
        Assert.All(
            result.DomainOutputs,
            output => Assert.Equal(
                DomainOutputValueKind.Unknown,
                output.Value.Kind));
    }

    [Fact]
    public void Missing_required_module_sum_is_incomplete()
    {
        var facts = ZeroFacts();
        facts.Remove("module_4_sum");

        var result = Evaluate(facts);

        Assert.Equal(AssessmentOutcome.Incomplete, result.Outcome);
        Assert.Contains("module_4_sum", result.MissingRequiredFields);
        Assert.Equal(
            ConditionResult.Unknown,
            result.RuleTrace!.ConditionResult);
    }

    [Fact]
    public void Score_below_12_5_only_means_the_ordinary_score_threshold_is_not_reached()
    {
        var result = Evaluate(ZeroFacts());

        Assert.Equal(AssessmentOutcome.NotSupported, result.Outcome);
        Assert.Equal(
            "NOT_REACHED",
            Threshold(result, "score_threshold_12_5").Value.Choice);
        Assert.Equal(0m, result.RuleTrace!.Condition.Actual!.Value.Number);
    }

    [Fact]
    public void Rule_and_outputs_are_inactive_before_the_pinned_effective_date()
    {
        var result = _evaluator.Evaluate(
            Load(),
            ZeroFacts(),
            EffectiveDate.AddDays(-1));

        Assert.Equal(AssessmentOutcome.HumanReview, result.Outcome);
        Assert.Null(result.RuleTrace);
        Assert.Empty(result.DomainOutputs);
    }

    [Fact]
    public void Exact_effective_date_activates_the_score_contract()
    {
        var facts = ZeroFacts();
        facts["module_4_sum"] = 3m;
        facts["module_1_sum"] = 2m;

        var result = _evaluator.Evaluate(
            Load(),
            facts,
            EffectiveDate);

        Assert.Equal(12.5m, result.RuleTrace!.Condition.Actual!.Value.Number);
        Assert.Equal(AssessmentOutcome.Supported, result.Outcome);
    }

    public static IEnumerable<object[]> ModuleBandCases()
    {
        foreach (var row in Bands(1,
            (0, 1, 0m),
            (2, 3, 2.5m),
            (4, 5, 5m),
            (6, 9, 7.5m),
            (10, 15, 10m)))
            yield return row;

        foreach (var row in Bands(2,
            (0, 1, 0m),
            (2, 5, 3.75m),
            (6, 10, 7.5m),
            (11, 16, 11.25m),
            (17, 33, 15m)))
            yield return row;

        foreach (var row in Bands(3,
            (0, 0, 0m),
            (1, 2, 3.75m),
            (3, 4, 7.5m),
            (5, 6, 11.25m),
            (7, 65, 15m)))
            yield return row;

        foreach (var row in Bands(4,
            (0, 2, 0m),
            (3, 7, 10m),
            (8, 18, 20m),
            (19, 36, 30m),
            (37, 54, 40m)))
            yield return row;

        foreach (var row in Bands(5,
            (0, 0, 0m),
            (1, 1, 5m),
            (2, 3, 10m),
            (4, 5, 15m),
            (6, 15, 20m)))
            yield return row;

        foreach (var row in Bands(6,
            (0, 0, 0m),
            (1, 3, 3.75m),
            (4, 6, 7.5m),
            (7, 11, 11.25m),
            (12, 18, 15m)))
            yield return row;
    }

    public static IEnumerable<object[]> TotalScoreCases()
    {
        yield return ScoreCase(
            0, 0, 0, 0, 7,
            11.25m,
            "NOT_REACHED", "NOT_REACHED", "NOT_REACHED", "NOT_REACHED", "NOT_REACHED");
        yield return ScoreCase(
            0, 0, 0, 1, 4,
            12.5m,
            "REACHED", "NOT_REACHED", "NOT_REACHED", "NOT_REACHED", "NOT_REACHED");
        yield return ScoreCase(
            0, 0, 0, 4, 7,
            26.25m,
            "REACHED", "NOT_REACHED", "NOT_REACHED", "NOT_REACHED", "NOT_REACHED");
        yield return ScoreCase(
            0, 0, 0, 6, 4,
            27.5m,
            "REACHED", "REACHED", "NOT_REACHED", "NOT_REACHED", "NOT_REACHED");
        yield return ScoreCase(
            0, 0, 8, 4, 7,
            46.25m,
            "REACHED", "REACHED", "NOT_REACHED", "NOT_REACHED", "NOT_REACHED");
        yield return ScoreCase(
            0, 0, 8, 6, 4,
            47.5m,
            "REACHED", "REACHED", "REACHED", "NOT_REACHED", "NOT_REACHED");
        yield return ScoreCase(
            0, 2, 19, 6, 12,
            68.75m,
            "REACHED", "REACHED", "REACHED", "NOT_REACHED", "NOT_REACHED");
        yield return ScoreCase(
            0, 0, 37, 4, 12,
            70m,
            "REACHED", "REACHED", "REACHED", "REACHED", "NOT_REACHED");
        yield return ScoreCase(
            2, 11, 37, 6, 12,
            88.75m,
            "REACHED", "REACHED", "REACHED", "REACHED", "NOT_REACHED");
        yield return ScoreCase(
            0, 17, 37, 6, 12,
            90m,
            "REACHED", "REACHED", "REACHED", "REACHED", "REACHED");
        yield return ScoreCase(
            10, 17, 37, 6, 12,
            100m,
            "REACHED", "REACHED", "REACHED", "REACHED", "REACHED");
    }

    private static IEnumerable<object[]> Bands(
        int module,
        params (int Minimum, int Maximum, decimal Weighted)[] bands)
    {
        foreach (var (minimum, maximum, weighted) in bands)
        {
            yield return [module, (decimal)minimum, weighted];
            if (maximum != minimum)
                yield return [module, (decimal)maximum, weighted];
        }
    }

    private static object[] ScoreCase(
        decimal module1,
        decimal module2,
        decimal module4,
        decimal module5,
        decimal module6,
        decimal expectedTotal,
        string threshold12_5,
        string threshold27,
        string threshold47_5,
        string threshold70,
        string threshold90)
        => [
            module1,
            module2,
            module4,
            module5,
            module6,
            expectedTotal,
            threshold12_5,
            threshold27,
            threshold47_5,
            threshold70,
            threshold90
        ];

    private static decimal? WeightedValue(
        NumericExpressionTrace total,
        int module)
        => module switch
        {
            1 => total.Children[0].Value.Number,
            2 => total.Children[1].Children[0].Value.Number,
            3 => total.Children[1].Children[1].Value.Number,
            4 => total.Children[2].Value.Number,
            5 => total.Children[3].Value.Number,
            6 => total.Children[4].Value.Number,
            _ => throw new ArgumentOutOfRangeException(nameof(module))
        };

    private static string FieldFor(int module)
        => $"module_{module}_sum";

    private AssessmentResult Evaluate(
        Dictionary<string, CaseValue> facts)
        => _evaluator.Evaluate(
            Load(),
            facts,
            new DateOnly(2026, 10, 3));

    private static DomainOutputTrace Threshold(
        AssessmentResult result,
        string outputId)
        => Assert.Single(
            result.DomainOutputs,
            output => output.OutputId == outputId);

    private NormaCase.Knowledge.Model.KnowledgePack Load()
        => _loader.LoadFromFile(
            Path.Combine(
                AppContext.BaseDirectory,
                "Fixtures",
                "pflege-adult-score-pack.json"));

    private static Dictionary<string, CaseValue> ZeroFacts()
        => new()
        {
            ["module_1_sum"] = 0m,
            ["module_2_sum"] = 0m,
            ["module_3_sum"] = 0m,
            ["module_4_sum"] = 0m,
            ["module_5_sum"] = 0m,
            ["module_6_sum"] = 0m
        };
}
