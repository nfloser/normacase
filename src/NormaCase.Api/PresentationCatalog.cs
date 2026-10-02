using System.Text.Json;
using System.Text.Json.Serialization;
using NormaCase.Knowledge.Model;
using NormaCase.Serialization;
using NormaCase.RuleEngine.Evaluation;
using NormaCase.Serialization;

namespace NormaCase.Api;

internal sealed record PresentationText(string Label, string? HelpText = null);
internal sealed record PresentationExample(string Id, string Label, string CaseFile);
internal sealed record PresentationDocument(
    int FormatVersion,
    string Locale,
    string PackId,
    string Name,
    string Description,
    Dictionary<string, PresentationText> Fields,
    Dictionary<string, PresentationText> EvidenceRequirements,
    Dictionary<string, PresentationText> Outputs,
    List<PresentationExample> Examples);

internal sealed record LoadedExample(string Id, string Label, CaseInput Input);
internal sealed record LoadedPresentation(
    string Locale,
    string Name,
    string Description,
    IReadOnlyDictionary<string, PresentationText> Fields,
    IReadOnlyDictionary<string, PresentationText> EvidenceRequirements,
    IReadOnlyDictionary<string, PresentationText> Outputs,
    IReadOnlyList<LoadedExample> Examples);

internal static class PresentationCatalog
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true,
        MaxDepth = 32
    };

    public static IReadOnlyDictionary<string, LoadedPresentation> Load(
        IReadOnlyDictionary<string, KnowledgePack> packs)
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "Presentation");
        var exampleDirectory = Path.Combine(AppContext.BaseDirectory, "Examples");
        var result = new Dictionary<string, LoadedPresentation>(StringComparer.Ordinal);

        foreach (var path in Directory.GetFiles(directory, "*.json", SearchOption.AllDirectories))
        {
            var json = File.ReadAllText(path);
            RejectDuplicateProperties(json);
            var document = JsonSerializer.Deserialize<PresentationDocument>(json, JsonOptions)
                ?? throw new InvalidOperationException("Presentation metadata is required.");

            if (document.FormatVersion != 1
                || !string.Equals(document.Locale, "de-DE", StringComparison.Ordinal)
                || string.IsNullOrWhiteSpace(document.Name)
                || string.IsNullOrWhiteSpace(document.Description)
                || !packs.TryGetValue(document.PackId, out var pack)
                || !result.TryAdd(document.PackId, null!))
            {
                throw new InvalidOperationException("Invalid synthetic presentation metadata.");
            }

            ValidateKeys(pack.Fields.Select(field => field.Id), document.Fields.Keys);
            ValidateKeys(pack.EvidenceRequirements.Select(item => item.Id), document.EvidenceRequirements.Keys);
            ValidateKeys(pack.Outputs.Select(item => item.Id).Distinct(StringComparer.Ordinal), document.Outputs.Keys);

            foreach (var item in document.Fields.Values
                         .Concat(document.EvidenceRequirements.Values)
                         .Concat(document.Outputs.Values))
            {
                if (string.IsNullOrWhiteSpace(item.Label))
                    throw new InvalidOperationException("Presentation labels must not be empty.");
            }

            var examples = new List<LoadedExample>();
            var exampleIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var example in document.Examples)
            {
                if (string.IsNullOrWhiteSpace(example.Id)
                    || string.IsNullOrWhiteSpace(example.Label)
                    || !exampleIds.Add(example.Id)
                    || Path.GetFileName(example.CaseFile) != example.CaseFile)
                {
                    throw new InvalidOperationException("Invalid synthetic example metadata.");
                }

                var examplePath = Path.Combine(exampleDirectory, example.CaseFile);
                var exampleJson = File.ReadAllText(examplePath);
                var input = CaseInputJson.Deserialize(exampleJson);
                _ = new RuleEvaluator().Evaluate(pack, input.Facts, input.AssessmentDate, input.Evidence);
                examples.Add(new(example.Id, example.Label, input));
            }

            result[document.PackId] = new(
                document.Locale,
                document.Name,
                document.Description,
                new Dictionary<string, PresentationText>(document.Fields, StringComparer.Ordinal),
                new Dictionary<string, PresentationText>(document.EvidenceRequirements, StringComparer.Ordinal),
                new Dictionary<string, PresentationText>(document.Outputs, StringComparer.Ordinal),
                examples);
        }

        if (result.Count != packs.Count || packs.Keys.Any(packId => !result.ContainsKey(packId)))
            throw new InvalidOperationException("Every synthetic pack requires de-DE presentation metadata.");

        return result;
    }

    private static void ValidateKeys(IEnumerable<string> expected, IEnumerable<string> actual)
    {
        var expectedSet = expected.ToHashSet(StringComparer.Ordinal);
        var actualSet = actual.ToHashSet(StringComparer.Ordinal);
        if (!expectedSet.SetEquals(actualSet))
            throw new InvalidOperationException("Presentation metadata ids must match the synthetic pack.");
    }

    private static void RejectDuplicateProperties(string json)
    {
        using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 32 });
        Visit(document.RootElement);
    }

    private static void Visit(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                if (!names.Add(property.Name))
                    throw new JsonException("Duplicate presentation property.");
                Visit(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
                Visit(item);
        }
    }
}
