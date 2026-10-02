using NormaCase.Domain.Decision;

namespace NormaCase.RuleEngine.Evaluation;

public enum StructuredOutputRole
{
    Decision = 0,
    Workflow = 1,
    Information = 2
}

public enum StructuredOutputValueKind
{
    Unknown = 0,
    Truth = 1,
    Number = 2,
    Code = 3
}

public readonly record struct StructuredOutputValue
{
    private StructuredOutputValue(
        StructuredOutputValueKind kind,
        TruthValue? truth,
        decimal? number,
        string? code)
    {
        Kind = kind;
        Truth = truth;
        Number = number;
        Code = code;
    }

    public StructuredOutputValueKind Kind { get; }
    public TruthValue? Truth { get; }
    public decimal? Number { get; }
    public string? Code { get; }

    public static StructuredOutputValue Unknown => default;

    public static StructuredOutputValue FromTruth(TruthValue value)
        => value == TruthValue.Unknown
            ? Unknown
            : new(StructuredOutputValueKind.Truth, value, null, null);

    public static StructuredOutputValue FromNumber(decimal value)
        => new(StructuredOutputValueKind.Number, null, value, null);

    public static StructuredOutputValue FromCode(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return new(StructuredOutputValueKind.Code, null, null, value);
    }
}

public sealed record StructuredOutputTrace(
    string Id,
    string? Scope,
    StructuredOutputRole Role,
    StructuredOutputValue Value,
    string RuleId,
    int RuleVersion,
    ConditionTrace Condition,
    SourceTrace Source);
