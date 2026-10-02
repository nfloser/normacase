using NormaCase.Knowledge.Model;
using NormaCase.Knowledge.Serialization;

namespace NormaCase.Api;

public sealed class SyntheticPackCatalog
{
    private readonly IReadOnlyDictionary<string, KnowledgePack> _packs;

    private SyntheticPackCatalog(
        IReadOnlyDictionary<string, KnowledgePack> packs)
    {
        _packs = packs;
    }

    public IReadOnlyList<PackCatalogItem> Items
        => _packs.Values
            .OrderBy(pack => pack.Manifest.PackId, StringComparer.Ordinal)
            .Select(ToCatalogItem)
            .ToArray();

    public bool TryGet(
        string packId,
        out KnowledgePack pack)
        => _packs.TryGetValue(packId, out pack!);

    public static SyntheticPackCatalog Load(string knowledgeRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(knowledgeRoot);

        var root = Path.GetFullPath(knowledgeRoot);
        if (!Directory.Exists(root))
            throw new DirectoryNotFoundException();

        var loader = new KnowledgePackLoader();
        var packs = new Dictionary<string, KnowledgePack>(StringComparer.Ordinal);

        foreach (var directory in Directory
            .EnumerateDirectories(root)
            .OrderBy(path => path, StringComparer.Ordinal))
        {
            var packPath = Path.Combine(directory, "pack.json");
            if (!File.Exists(packPath))
                continue;

            var pack = loader.LoadFromFile(packPath);
            if (!string.Equals(
                pack.Manifest.ValidationLevel,
                "SYNTHETIC",
                StringComparison.Ordinal))
            {
                continue;
            }

            if (!packs.TryAdd(pack.Manifest.PackId, pack))
                throw new InvalidDataException("Duplicate synthetic pack id.");
        }

        if (packs.Count == 0)
            throw new InvalidDataException("No synthetic Knowledge Packs available.");

        return new(packs);
    }

    private static PackCatalogItem ToCatalogItem(KnowledgePack pack)
        => new(
            pack.Manifest.PackId,
            pack.Manifest.ReleaseId,
            pack.Manifest.LifecycleStatus,
            pack.Manifest.ValidationLevel,
            pack.Fields
                .Select(field => new PackFieldItem(
                    field.Id,
                    field.Type,
                    field.Required))
                .ToArray(),
            pack.EvidenceRequirements
                .Select(evidence => new PackEvidenceItem(evidence.Id))
                .ToArray());
}
