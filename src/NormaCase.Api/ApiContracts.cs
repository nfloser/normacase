namespace NormaCase.Api;

public sealed record PackCatalogResponse(
    IReadOnlyList<PackCatalogItem> Packs);

public sealed record PackCatalogItem(
    string PackId,
    string ReleaseId,
    string LifecycleStatus,
    string ValidationLevel,
    IReadOnlyList<PackFieldItem> Fields,
    IReadOnlyList<PackEvidenceItem> EvidenceRequirements);

public sealed record PackFieldItem(
    string Id,
    string Type,
    bool Required);

public sealed record PackEvidenceItem(string Id);

public sealed record ApiError(
    string Code,
    string Message);
