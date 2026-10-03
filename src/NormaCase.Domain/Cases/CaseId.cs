namespace NormaCase.Domain.Cases;

public readonly record struct CaseId
{
    public CaseId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Case id must not be blank.", nameof(value));

        Value = value;
    }

    public string Value { get; }

    public bool IsEmpty => string.IsNullOrEmpty(Value);

    public override string ToString() => Value ?? string.Empty;
}
