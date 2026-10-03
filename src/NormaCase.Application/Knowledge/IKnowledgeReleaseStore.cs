using NormaCase.Knowledge.Catalog;

namespace NormaCase.Application.Knowledge;

/// <summary>Immutable exact release artifacts; registration never activates or approves knowledge.</summary>
public interface IKnowledgeReleaseStore
{
    Task<KnowledgeReleaseArtifact> RegisterAsync(string knowledgePackJson, CancellationToken token = default);
    Task<KnowledgeReleaseArtifact?> LoadAsync(string packId, string releaseId, CancellationToken token = default);
}
