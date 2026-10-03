using System.Text.Json;
using NormaCase.Application.Assessments;
using NormaCase.Application.Intake;
using NormaCase.Domain.Cases;

namespace NormaCase.Serialization;

public static class NormalizedIntakeJson
{
    private sealed record Document(int FormatVersion, CaseId CaseId, string CaseTypeId, IntakeProvenance Provenance,
        string KnowledgePackId, string KnowledgeRelease, AssessmentInputSnapshot Input, IReadOnlyDictionary<string, IReadOnlyList<string>> EvidenceReferences);
    public static string Serialize(NormalizedIntakeRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        return JsonSerializer.Serialize(new Document(1, record.CaseId, record.CaseTypeId, record.Provenance,
            record.KnowledgePackId, record.KnowledgeRelease, record.Input, record.EvidenceReferences), InterchangeJson.Options);
    }
    public static NormalizedIntakeRecord Deserialize(string json)
    {
        var document = InterchangeJson.Read<Document>(json);
        if (document.FormatVersion != 1) throw new JsonException("Unsupported normalized intake format.");
        try
        {
            return NormalizedIntakeRecord.Restore(document.CaseId, document.CaseTypeId, document.Provenance,
            document.KnowledgePackId, document.KnowledgeRelease, document.Input, document.EvidenceReferences);
        }
        catch (ArgumentException) { throw new JsonException("Invalid normalized intake history."); }
    }
}
