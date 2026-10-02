namespace NormaCase.Knowledge.Validation;

public sealed record KnowledgeValidationError(
    string Code,
    string Message,
    string Path);
