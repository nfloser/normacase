using System.Globalization;
using NormaCase.Application.Knowledge;
using NormaCase.Knowledge.Model;
using NormaCase.Persistence.PostgreSql;

namespace NormaCase.Api;

internal static class SyntheticIntakeKnowledge
{
    internal static async Task<KnowledgePack> LoadAsync(IConfiguration configuration, Npgsql.NpgsqlDataSource source,
        KnowledgePack installed, CancellationToken token = default)
    {
        var section = configuration.GetSection("SyntheticReview:IntakeKnowledgeActivation");
        if (!section.Exists()) return installed;
        var allowedKeys = new HashSet<string>(["PackId", "Revision", "ReleaseId", "Sha256"], StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrEmpty(section.Value) || section.GetChildren().Any(item => !allowedKeys.Contains(item.Key)))
            throw new InvalidOperationException("Invalid exact intake Knowledge selection.");
        var text = section["Revision"];
        if (!long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var revision)
            || revision < 1 || revision.ToString(CultureInfo.InvariantCulture) != text)
            throw new InvalidOperationException("Invalid exact intake Knowledge revision.");
        var selection = new KnowledgeActivationSelection(section["PackId"]!, revision, section["ReleaseId"]!, section["Sha256"]!);
        var artifact = await new KnowledgeActivationSelectionService(new PostgresReviewedKnowledgeActivationStore(source),
            new PostgresKnowledgeReleaseStore(source), new PostgresKnowledgeEvidenceStore(source)).LoadAsync(selection, token);
        var selected = artifact.LoadPack();
        // This adapter's configured German field/evidence presentation is tied to its
        // installed synthetic schema. Other schemas need their own explicit adapter mapping.
        if (selected.Manifest.LifecycleStatus != "ACTIVE" || selected.Manifest.ValidationLevel != "SYNTHETIC" || selected.Manifest.PackId != installed.Manifest.PackId
            || !selected.Fields.OrderBy(f => f.Id, StringComparer.Ordinal).Select(f => (f.Id, f.Type))
                .SequenceEqual(installed.Fields.OrderBy(f => f.Id, StringComparer.Ordinal).Select(f => (f.Id, f.Type)))
            || !selected.EvidenceRequirements.Select(e => e.Id).Order(StringComparer.Ordinal)
                .SequenceEqual(installed.EvidenceRequirements.Select(e => e.Id).Order(StringComparer.Ordinal))
            || selected.Outputs.Count != 0 || selected.Workflows.Count != 0)
            throw new InvalidOperationException("Selected release is incompatible with the synthetic intake adapter.");
        return selected;
    }
}
