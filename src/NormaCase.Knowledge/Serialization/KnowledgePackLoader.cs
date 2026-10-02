using System.Text.Json;
using System.Text.Json.Serialization;
using NormaCase.Knowledge.Model;
using NormaCase.Knowledge.Validation;

namespace NormaCase.Knowledge.Serialization;

public sealed class KnowledgePackLoader
{
    private static readonly JsonSerializerOptions JsonOptions = CreateOptions();

    private readonly KnowledgePackValidator _validator;

    public KnowledgePackLoader(KnowledgePackValidator? validator = null)
    {
        _validator = validator ?? new KnowledgePackValidator();
    }

    public KnowledgePack LoadFromFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return LoadFromJson(File.ReadAllText(path));
    }

    public KnowledgePack LoadFromJson(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 64 });
        ValidateJsonStructure(document.RootElement);

        var pack = JsonSerializer.Deserialize<KnowledgePack>(json, JsonOptions)
            ?? throw new JsonException("Knowledge Pack JSON did not contain an object.");

        _validator.ValidateOrThrow(pack);
        return pack;
    }

    private static void ValidateJsonStructure(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in element.EnumerateObject())
            {
                if (!names.Add(property.Name))
                    throw new JsonException("Duplicate Knowledge Pack properties are not allowed.");
                ValidateJsonStructure(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.Null)
                    throw new JsonException("Knowledge Pack arrays cannot contain null entries.");
                ValidateJsonStructure(item);
            }
        }
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            RespectNullableAnnotations = true,
            NumberHandling = JsonNumberHandling.Strict
        };

        options.Converters.Add(
            new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper, allowIntegerValues: false));

        return options;
    }
}
