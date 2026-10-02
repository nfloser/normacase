namespace NormaCase.Domain.Decision;

public enum DomainOutputValueKind
{
    Unknown,
    Choice
}

public readonly record struct DomainOutputValue
{
    private DomainOutputValue(
        DomainOutputValueKind kind,
        string? choice)
    {
        Kind = kind;
        Choice = choice;
    }

    public DomainOutputValueKind Kind { get; }

    public string? Choice { get; }

    public bool IsUnknown => Kind == DomainOutputValueKind.Unknown;

    public static DomainOutputValue Unknown { get; }
        = new(DomainOutputValueKind.Unknown, null);

    public static DomainOutputValue FromChoice(string choice)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(choice);

        if (string.Equals(choice, "UNKNOWN", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "UNKNOWN is reserved for the explicit unknown state.",
                nameof(choice));
        }

        return new(DomainOutputValueKind.Choice, choice);
    }
}
