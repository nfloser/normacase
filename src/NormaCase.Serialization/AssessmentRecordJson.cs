using System.Text.Json;
using NormaCase.Application.Assessments;

namespace NormaCase.Serialization;

public sealed record AssessmentRecordDocument(
    int FormatVersion,
    AssessmentRecord Record);

public static class AssessmentRecordJson
{
    public const int CurrentFormatVersion = 1;

    public static string Serialize(AssessmentRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        return JsonSerializer.Serialize(
            new AssessmentRecordDocument(CurrentFormatVersion, record),
            InterchangeJson.Options);
    }

    public static AssessmentRecord Deserialize(string json)
    {
        try
        {
            var document = InterchangeJson.Read<AssessmentRecordDocument>(json);
            if (document.FormatVersion != CurrentFormatVersion)
                throw new JsonException("Unsupported assessment record format.");

            return document.Record;
        }
        catch (JsonException)
        {
            throw;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            throw new JsonException("Invalid assessment record.", exception);
        }
    }
}
