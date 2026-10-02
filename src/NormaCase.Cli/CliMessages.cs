using System.Globalization;
using System.Resources;

namespace NormaCase.Cli;

internal static class CliMessages
{
    private static readonly ResourceManager Resources =
        new("NormaCase.Cli.Resources.Messages", typeof(CliMessages).Assembly);

    public static string Get(string key)
        => Resources.GetString(key, CultureInfo.GetCultureInfo("de-DE"))
            ?? throw new InvalidOperationException($"Missing CLI resource '{key}'.");
}
