using System.Text.RegularExpressions;

namespace NormaCase.Assessments;

internal static partial class TechnicalIdentifier
{
    internal const int MaximumLength = 128;

    [GeneratedRegex(
        "^[A-Za-z0-9][A-Za-z0-9._:-]{0,127}$",
        RegexOptions.CultureInvariant)]
    private static partial Regex Pattern();

    internal static string Require(
        string value,
        string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        if (value.Length > MaximumLength
            || !Pattern().IsMatch(value))
        {
            throw new ArgumentException(
                "Technical identifier has an invalid format.",
                parameterName);
        }

        return value;
    }

    internal static DateTimeOffset RequireUtc(
        DateTimeOffset value,
        string parameterName)
    {
        if (value.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException(
                "Timestamp must use UTC offset zero.",
                parameterName);
        }

        return value;
    }
}
