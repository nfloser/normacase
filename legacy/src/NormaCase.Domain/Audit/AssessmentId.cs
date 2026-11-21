namespace NormaCase.Domain.Audit;

public readonly record struct AssessmentId
{
    public AssessmentId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Assessment id must not be blank.", nameof(value));

        Value = value;
    }

    public string Value { get; }

    public bool IsEmpty => string.IsNullOrEmpty(Value);

    public override string ToString() => Value ?? string.Empty;
}
