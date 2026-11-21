using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using NormaCase.Application.Outbound;

namespace NormaCase.Serialization;
public static class ReviewedCaseResultJson
{
    public const int CurrentFormatVersion=1;
    private static readonly JsonSerializerOptions Options=CreateOptions();
    public static string Serialize(ReviewedCaseResult result)
    {
        ArgumentNullException.ThrowIfNull(result);result.Validate();
        return JsonSerializer.Serialize(new ResultDocument(CurrentFormatVersion,result),Options);
    }
    public static ReviewedCaseResult Deserialize(string json)
    {
        var document=InterchangeJson.Read<ResultDocument>(json,Options);
        if(document.FormatVersion!=CurrentFormatVersion)throw new JsonException("Unsupported outbound result format.");
        try{document.Result.Validate();return document.Result;}
        catch(ArgumentException){throw new JsonException("Invalid outbound result binding.");}
    }
    private static JsonSerializerOptions CreateOptions()
    {
        var options=new JsonSerializerOptions(InterchangeJson.Options);
        options.Converters.Add(new RevisionConverter());return options;
    }
    private sealed record ResultDocument(int FormatVersion,ReviewedCaseResult Result);
    private sealed class RevisionConverter:JsonConverter<long>
    {
        public override long Read(ref Utf8JsonReader reader,Type type,JsonSerializerOptions options)
        {
            if(reader.TokenType!=JsonTokenType.String)throw new JsonException("Outbound revisions must be exact strings.");
            var value=reader.GetString();
            if(!long.TryParse(value,NumberStyles.None,CultureInfo.InvariantCulture,out var parsed)
                ||parsed.ToString(CultureInfo.InvariantCulture)!=value)
                throw new JsonException("Invalid outbound revision.");
            return parsed;
        }
        public override void Write(Utf8JsonWriter writer,long value,JsonSerializerOptions options)
            =>writer.WriteStringValue(value.ToString(CultureInfo.InvariantCulture));
    }
}
