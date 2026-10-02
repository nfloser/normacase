namespace NormaCase.Api;

public sealed record ApiHostOptions(
    string KnowledgeRoot,
    string PlatformVersion,
    int Port = 5099);
