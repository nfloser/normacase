using System.Text.Json;
using NormaCase.Domain.Cases;
using NormaCase.Domain.Evidence;

namespace NormaCase.Serialization;

public sealed record CaseInput(
    int FormatVersion,
    DateOnly AssessmentDate,
    IReadOnlyDictionary<string, CaseValue> Facts,
    IReadOnlyDictionary<string, EvidenceStatus>? Evidence = null);

public static class CaseInputJson
{
    public static CaseInput Deserialize(string json)
    {
        var input = InterchangeJson.Read<CaseInput>(json);
        if (input.FormatVersion != 1)
            throw new JsonException("Unsupported case input format.");
        return input;
    }
}
