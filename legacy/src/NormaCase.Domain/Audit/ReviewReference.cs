namespace NormaCase.Domain.Audit;

public sealed record ReviewReference
{
    public ReviewReference(string kind, string value)
    {
        if (string.IsNullOrWhiteSpace(kind))
            throw new ArgumentException("Reference kind must not be blank.", nameof(kind));
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Reference value must not be blank.", nameof(value));

        Kind = kind;
        Value = value;
    }

    public string Kind { get; }

    public string Value { get; }
}
