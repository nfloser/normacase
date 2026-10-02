using System.Globalization;
using System.Resources;

namespace NormaCase.Cli;

internal static class Messages
{
    private static readonly ResourceManager Resources = new("NormaCase.Cli.Resources.Messages", typeof(Messages).Assembly);
    internal static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("de-DE");
    internal static string Get(string key) => Resources.GetString(key, Culture)
        ?? throw new InvalidOperationException("Missing presentation resource.");
}
