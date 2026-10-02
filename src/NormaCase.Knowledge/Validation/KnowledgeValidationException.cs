namespace NormaCase.Knowledge.Validation;

public sealed class KnowledgeValidationException : Exception
{
    public KnowledgeValidationException(IReadOnlyList<KnowledgeValidationError> errors)
        : base($"Knowledge Pack validation failed with {errors.Count} error(s).")
    {
        Errors = errors;
    }

    public IReadOnlyList<KnowledgeValidationError> Errors { get; }
}
