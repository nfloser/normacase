using System.Globalization;
using System.Resources;

namespace NormaCase.Api;

internal static class ApiMessages
{
    private static readonly ResourceManager Resources = new("NormaCase.Api.Resources.Messages", typeof(ApiMessages).Assembly);
    internal static string Get(string code) => Resources.GetString(code, CultureInfo.GetCultureInfo("de-DE"))
        ?? throw new InvalidOperationException("Missing error resource.");
}
