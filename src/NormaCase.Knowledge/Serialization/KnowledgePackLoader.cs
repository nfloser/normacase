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

        var pack = JsonSerializer.Deserialize<KnowledgePack>(json, JsonOptions)
            ?? throw new JsonException("Knowledge Pack JSON did not contain an object.");

        _validator.ValidateOrThrow(pack);
        return pack;
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true
        };

        options.Converters.Add(
            new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper));

        return options;
    }
}
