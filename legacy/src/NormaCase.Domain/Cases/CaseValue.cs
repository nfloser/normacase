using NormaCase.Domain.Decision;

namespace NormaCase.Domain.Cases;

public enum CaseValueKind
{
    Unknown = 0,
    Truth = 1,
    Number = 2
}

/// <summary>
/// Typed value supplied to deterministic rule evaluation.
/// The default value is deliberately Unknown so uninitialized input fails closed.
/// </summary>
public readonly record struct CaseValue
{
    private CaseValue(
        CaseValueKind kind,
        TruthValue? truth,
        decimal? number)
    {
        Kind = kind;
        Truth = truth;
        Number = number;
    }

    public CaseValueKind Kind { get; }
    public TruthValue? Truth { get; }
    public decimal? Number { get; }

    public bool IsUnknown => Kind == CaseValueKind.Unknown;

    public static CaseValue Unknown => default;

    public static CaseValue FromTruth(TruthValue value)
        => value == TruthValue.Unknown
            ? Unknown
            : new(CaseValueKind.Truth, value, null);

    public static CaseValue FromNumber(decimal value)
        => new(CaseValueKind.Number, null, value);

    public static implicit operator CaseValue(TruthValue value)
        => FromTruth(value);

    public static implicit operator CaseValue(decimal value)
        => FromNumber(value);

    public static implicit operator CaseValue(int value)
        => FromNumber(value);
}
