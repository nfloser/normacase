namespace NormaCase.Domain.Workflow;

public readonly record struct WorkflowRunId
{
    public WorkflowRunId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Workflow run id must not be blank.", nameof(value));

        Value = value;
    }

    public string Value { get; }
    public bool IsEmpty => string.IsNullOrEmpty(Value);
    public override string ToString() => Value ?? string.Empty;
}
