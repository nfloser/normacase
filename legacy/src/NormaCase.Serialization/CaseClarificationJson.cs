using System.Text.Json;
using NormaCase.Application.Corrections;

namespace NormaCase.Serialization;

public sealed record CaseClarificationDocument(int FormatVersion, CaseClarificationRecord Request);
public static class CaseClarificationJson
{
    public static string Serialize(CaseClarificationRecord request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return JsonSerializer.Serialize(new CaseClarificationDocument(1, request), InterchangeJson.Options);
    }
    public static CaseClarificationRecord Deserialize(string json)
    {
        var document = InterchangeJson.Read<CaseClarificationDocument>(json);
        if (document.FormatVersion != 1) throw new JsonException("Unsupported clarification format.");
        return document.Request;
    }
}
