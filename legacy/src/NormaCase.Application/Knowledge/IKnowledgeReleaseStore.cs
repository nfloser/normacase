using NormaCase.Knowledge.Catalog;

namespace NormaCase.Application.Knowledge;

/// <summary>Immutable exact release artifacts; registration never activates or approves knowledge.</summary>
public interface IKnowledgeReleaseStore
{
    Task<KnowledgeReleaseArtifact> RegisterAsync(string knowledgePackJson, CancellationToken token = default);
    // One extra row signals continuation; stable ordinal tuple cursor and optional exact filters.
    Task<IReadOnlyList<KnowledgeReleaseArtifact>> ListAsync(int pageSize, string? packId = null,
        string? afterPackId = null, string? afterReleaseId = null, string? validationLevel = null,
        CancellationToken token = default);
    Task<KnowledgeReleaseArtifact?> LoadAsync(string packId, string releaseId, CancellationToken token = default);
}
