using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using NormaCase.Knowledge.Model;

namespace NormaCase.Knowledge.Presentation;

public sealed record KnowledgePresentationText(
    string Label,
    string? HelpText = null);

public sealed record KnowledgePresentationOutput
{
    internal KnowledgePresentationOutput(
        string label,
        IReadOnlyDictionary<string, string> choices)
    {
        Label = label;
        Choices = choices;
    }

    public string Label { get; }

    public IReadOnlyDictionary<string, string> Choices { get; }
}

public sealed record KnowledgePresentationExample(
    string Id,
    string Label,
    string CaseFile);

public sealed record KnowledgePresentation
{
    internal KnowledgePresentation(
        string locale,
        string packId,
        string name,
        string description,
        IReadOnlyDictionary<string, KnowledgePresentationText> fields,
        IReadOnlyDictionary<string, KnowledgePresentationText> evidenceRequirements,
        IReadOnlyDictionary<string, KnowledgePresentationOutput> outputs,
        IReadOnlyList<KnowledgePresentationExample> examples)
    {
        Locale = locale;
        PackId = packId;
        Name = name;
        Description = description;
        Fields = fields;
        EvidenceRequirements = evidenceRequirements;
        Outputs = outputs;
        Examples = examples;
    }

    public string Locale { get; }

    public string PackId { get; }

    public string Name { get; }

    public string Description { get; }

    public IReadOnlyDictionary<string, KnowledgePresentationText> Fields { get; }

    public IReadOnlyDictionary<string, KnowledgePresentationText>
        EvidenceRequirements { get; }

    public IReadOnlyDictionary<string, KnowledgePresentationOutput> Outputs { get; }

    public IReadOnlyList<KnowledgePresentationExample> Examples { get; }
}

public sealed class KnowledgePresentationLoader
{
    public const int CurrentFormatVersion = 1;

    private static readonly JsonSerializerOptions JsonOptions =
        CreateOptions();

    public KnowledgePresentation LoadFromFile(
        KnowledgePack pack,
        string path,
        string expectedLocale)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        return LoadFromJson(
            pack,
            File.ReadAllText(path),
            expectedLocale);
    }

    public KnowledgePresentation LoadFromJson(
        KnowledgePack pack,
        string json,
        string expectedLocale)
    {
        ArgumentNullException.ThrowIfNull(pack);
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedLocale);

        RejectDuplicateProperties(json);

        var document =
            JsonSerializer.Deserialize<PresentationDocument>(
                json,
                JsonOptions)
            ?? throw new JsonException(
                "Presentation metadata is required.");

        ValidateDocument(pack, document, expectedLocale);

        var fields = new ReadOnlyDictionary<
            string,
            KnowledgePresentationText>(
            new Dictionary<string, KnowledgePresentationText>(
                document.Fields,
                StringComparer.Ordinal));

        var evidence = new ReadOnlyDictionary<
            string,
            KnowledgePresentationText>(
            new Dictionary<string, KnowledgePresentationText>(
                document.EvidenceRequirements,
                StringComparer.Ordinal));

        var outputs = new Dictionary<
            string,
            KnowledgePresentationOutput>(
            StringComparer.Ordinal);

        foreach (var item in document.Outputs)
        {
            outputs.Add(
                item.Key,
                new(
                    item.Value.Label,
                    new ReadOnlyDictionary<string, string>(
                        new Dictionary<string, string>(
                            item.Value.Choices,
                            StringComparer.Ordinal))));
        }

        var examples = Array.AsReadOnly(
            document.Examples
                .Select(example => new KnowledgePresentationExample(
                    example.Id,
                    example.Label,
                    example.CaseFile))
                .ToArray());

        return new(
            document.Locale,
            document.PackId,
            document.Name,
            document.Description,
            fields,
            evidence,
            new ReadOnlyDictionary<
                string,
                KnowledgePresentationOutput>(outputs),
            examples);
    }

    private static void ValidateDocument(
        KnowledgePack pack,
        PresentationDocument document,
        string expectedLocale)
    {
        if (document.FormatVersion != CurrentFormatVersion
            || !string.Equals(
                document.Locale,
                expectedLocale,
                StringComparison.Ordinal)
            || !string.Equals(
                document.PackId,
                pack.Manifest.PackId,
                StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(document.Name)
            || string.IsNullOrWhiteSpace(document.Description))
        {
            throw new InvalidOperationException(
                "Invalid Knowledge presentation metadata.");
        }

        ValidateKeys(
            pack.Fields.Select(field => field.Id),
            document.Fields.Keys);

        ValidateKeys(
            pack.EvidenceRequirements.Select(item => item.Id),
            document.EvidenceRequirements.Keys);

        ValidateKeys(
            pack.Outputs
                .Select(item => item.Id)
                .Distinct(StringComparer.Ordinal),
            document.Outputs.Keys);

        foreach (var text in document.Fields.Values.Concat(
                     document.EvidenceRequirements.Values))
        {
            if (string.IsNullOrWhiteSpace(text.Label))
            {
                throw new InvalidOperationException(
                    "Presentation labels must not be empty.");
            }
        }

        foreach (var item in document.Outputs)
        {
            if (string.IsNullOrWhiteSpace(item.Value.Label)
                || item.Value.Choices.Any(
                    choice => string.IsNullOrWhiteSpace(
                        choice.Value)))
            {
                throw new InvalidOperationException(
                    "Output presentation labels must not be empty.");
            }

            var expectedChoices = pack.Outputs
                .Where(output => string.Equals(
                    output.Id,
                    item.Key,
                    StringComparison.Ordinal))
                .SelectMany(output => output.Choices)
                .ToHashSet(StringComparer.Ordinal);

            if (!expectedChoices.SetEquals(
                    item.Value.Choices.Keys))
            {
                throw new InvalidOperationException(
                    "Output presentation choices must match the Knowledge Pack.");
            }
        }

        var exampleIds = new HashSet<string>(
            StringComparer.Ordinal);

        foreach (var example in document.Examples)
        {
            if (string.IsNullOrWhiteSpace(example.Id)
                || string.IsNullOrWhiteSpace(example.Label)
                || string.IsNullOrWhiteSpace(example.CaseFile)
                || !exampleIds.Add(example.Id)
                || !string.Equals(
                    Path.GetFileName(example.CaseFile),
                    example.CaseFile,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Invalid presentation example metadata.");
            }
        }
    }

    private static void ValidateKeys(
        IEnumerable<string> expected,
        IEnumerable<string> actual)
    {
        var expectedSet =
            expected.ToHashSet(StringComparer.Ordinal);
        var actualSet =
            actual.ToHashSet(StringComparer.Ordinal);

        if (!expectedSet.SetEquals(actualSet))
        {
            throw new InvalidOperationException(
                "Presentation metadata ids must match the Knowledge Pack.");
        }
    }

    private static void RejectDuplicateProperties(string json)
    {
        using var document = JsonDocument.Parse(
            json,
            new JsonDocumentOptions
            {
                MaxDepth = 32
            });

        Visit(document.RootElement);
    }

    private static void Visit(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names =
                new HashSet<string>(StringComparer.Ordinal);

            foreach (var property in element.EnumerateObject())
            {
                if (!names.Add(property.Name))
                {
                    throw new JsonException(
                        "Duplicate presentation property.");
                }

                Visit(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                Visit(item);
            }
        }
    }

    private static JsonSerializerOptions CreateOptions()
        => new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = false,
            UnmappedMemberHandling =
                JsonUnmappedMemberHandling.Disallow,
            RespectNullableAnnotations = true,
            RespectRequiredConstructorParameters = true,
            MaxDepth = 32
        };

    private sealed record PresentationOutputData(
        string Label,
        Dictionary<string, string> Choices);

    private sealed record PresentationExampleData(
        string Id,
        string Label,
        string CaseFile);

    private sealed record PresentationDocument(
        int FormatVersion,
        string Locale,
        string PackId,
        string Name,
        string Description,
        Dictionary<string, KnowledgePresentationText> Fields,
        Dictionary<string, KnowledgePresentationText>
            EvidenceRequirements,
        Dictionary<string, PresentationOutputData> Outputs,
        List<PresentationExampleData> Examples);
}
