using NormaCase.Knowledge.Model;
using NormaCase.Knowledge.Presentation;
using NormaCase.RuleEngine.Evaluation;
using NormaCase.Serialization;

namespace NormaCase.Api;

internal sealed record LoadedExample(
    string Id,
    string Label,
    CaseInput Input);

internal sealed record LoadedPresentation(
    string Locale,
    string Name,
    string Description,
    IReadOnlyDictionary<string, KnowledgePresentationText> Fields,
    IReadOnlyDictionary<string, KnowledgePresentationText>
        EvidenceRequirements,
    IReadOnlyDictionary<string, KnowledgePresentationOutput> Outputs,
    IReadOnlyList<LoadedExample> Examples);

internal static class PresentationCatalog
{
    public static IReadOnlyDictionary<string, LoadedPresentation> Load(
        IReadOnlyDictionary<string, KnowledgePack> packs)
    {
        var directory = Path.Combine(
            AppContext.BaseDirectory,
            "Presentation");
        var exampleDirectory = Path.Combine(
            AppContext.BaseDirectory,
            "Examples");
        var result = new Dictionary<string, LoadedPresentation>(
            StringComparer.Ordinal);
        var loader = new KnowledgePresentationLoader();

        foreach (var path in Directory.GetFiles(
                     directory,
                     "*.json",
                     SearchOption.AllDirectories))
        {
            var metadata = loader.LoadFromJson(
                packs,
                File.ReadAllText(path),
                "de-DE");

            if (!result.TryAdd(metadata.PackId, null!))
            {
                throw new InvalidOperationException(
                    "Duplicate presentation metadata for Knowledge Pack.");
            }

            var pack = packs[metadata.PackId];
            var examples = new List<LoadedExample>();

            foreach (var example in metadata.Examples)
            {
                var examplePath = Path.Combine(
                    exampleDirectory,
                    example.CaseFile);
                var exampleJson = File.ReadAllText(examplePath);
                var input = CaseInputJson.Deserialize(exampleJson);

                _ = new RuleEvaluator().Evaluate(
                    pack,
                    input.Facts,
                    input.AssessmentDate,
                    input.Evidence);

                examples.Add(
                    new(
                        example.Id,
                        example.Label,
                        input));
            }

            result[metadata.PackId] = new(
                metadata.Locale,
                metadata.Name,
                metadata.Description,
                metadata.Fields,
                metadata.EvidenceRequirements,
                metadata.Outputs,
                examples);
        }

        if (result.Count != packs.Count
            || packs.Keys.Any(
                packId => !result.ContainsKey(packId)))
        {
            throw new InvalidOperationException(
                "Every synthetic pack requires de-DE presentation metadata.");
        }

        return result;
    }
}
