using System.Security.Cryptography;
using System.Text;
using NormaCase.Knowledge.Model;
using NormaCase.Knowledge.Serialization;

namespace NormaCase.Knowledge.Catalog;

public sealed class KnowledgeReleaseArtifact
{
    private readonly string _knowledgePackJson;

    internal KnowledgeReleaseArtifact(
        string knowledgePackJson,
        KnowledgePack pack)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(knowledgePackJson);
        ArgumentNullException.ThrowIfNull(pack);

        PackId = pack.Manifest.PackId;
        ReleaseId = pack.Manifest.ReleaseId;
        LifecycleStatus = pack.Manifest.LifecycleStatus;
        ValidationLevel = pack.Manifest.ValidationLevel;
        Sha256 = Convert.ToHexStringLower(
            SHA256.HashData(
                Encoding.UTF8.GetBytes(knowledgePackJson)));
        _knowledgePackJson = knowledgePackJson;
    }

    public string PackId { get; }

    public string ReleaseId { get; }

    public string LifecycleStatus { get; }

    public string ValidationLevel { get; }

    public string Sha256 { get; }

    public KnowledgePack LoadPack()
        => new KnowledgePackLoader().LoadFromJson(
            _knowledgePackJson);

    internal bool HasExactJson(string knowledgePackJson)
        => string.Equals(
            _knowledgePackJson,
            knowledgePackJson,
            StringComparison.Ordinal);
}

public sealed class KnowledgeReleaseCatalog
{
    private readonly object _gate = new();
    private readonly KnowledgePackLoader _loader;
    private readonly Dictionary<ReleaseKey, KnowledgeReleaseArtifact>
        _releases = [];

    public KnowledgeReleaseCatalog(
        KnowledgePackLoader? loader = null)
    {
        _loader = loader ?? new KnowledgePackLoader();
    }

    public IReadOnlyList<KnowledgeReleaseArtifact> Releases
    {
        get
        {
            lock (_gate)
            {
                return Array.AsReadOnly(
                    _releases.Values
                        .OrderBy(
                            item => item.PackId,
                            StringComparer.Ordinal)
                        .ThenBy(
                            item => item.ReleaseId,
                            StringComparer.Ordinal)
                        .ToArray());
            }
        }
    }

    public KnowledgeReleaseArtifact Register(
        string knowledgePackJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            knowledgePackJson);

        var pack = _loader.LoadFromJson(knowledgePackJson);
        var key = new ReleaseKey(
            pack.Manifest.PackId,
            pack.Manifest.ReleaseId);

        lock (_gate)
        {
            if (_releases.TryGetValue(key, out var existing))
            {
                if (existing.HasExactJson(knowledgePackJson))
                    return existing;

                throw new KnowledgeReleaseIdentityConflictException(
                    key.PackId,
                    key.ReleaseId);
            }

            var artifact = new KnowledgeReleaseArtifact(
                knowledgePackJson,
                pack);
            _releases.Add(key, artifact);
            return artifact;
        }
    }

    public KnowledgeReleaseArtifact Get(
        string packId,
        string releaseId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packId);
        ArgumentException.ThrowIfNullOrWhiteSpace(releaseId);

        lock (_gate)
        {
            if (_releases.TryGetValue(
                    new ReleaseKey(packId, releaseId),
                    out var artifact))
            {
                return artifact;
            }
        }

        throw new KnowledgeReleaseNotFoundException(
            packId,
            releaseId);
    }

    public bool TryGet(
        string packId,
        string releaseId,
        out KnowledgeReleaseArtifact? artifact)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packId);
        ArgumentException.ThrowIfNullOrWhiteSpace(releaseId);

        lock (_gate)
        {
            if (_releases.TryGetValue(
                    new ReleaseKey(packId, releaseId),
                    out var existing))
            {
                artifact = existing;
                return true;
            }

            artifact = null;
            return false;
        }
    }

    private readonly record struct ReleaseKey(
        string PackId,
        string ReleaseId);
}

public sealed class KnowledgeReleaseIdentityConflictException
    : InvalidOperationException
{
    public KnowledgeReleaseIdentityConflictException(
        string packId,
        string releaseId)
        : base(
            $"Knowledge Release '{packId}/{releaseId}' is already registered with different content.")
    {
        PackId = packId;
        ReleaseId = releaseId;
    }

    public string PackId { get; }

    public string ReleaseId { get; }
}

public sealed class KnowledgeReleaseNotFoundException
    : KeyNotFoundException
{
    public KnowledgeReleaseNotFoundException(
        string packId,
        string releaseId)
        : base(
            $"Knowledge Release '{packId}/{releaseId}' is not registered.")
    {
        PackId = packId;
        ReleaseId = releaseId;
    }

    public string PackId { get; }

    public string ReleaseId { get; }
}
