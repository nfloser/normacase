using NormaCase.Domain.Workflow;
using Xunit;

namespace NormaCase.Domain.Tests.Workflow;

public sealed class WorkflowRunIdTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t")]
    public void Blank_id_is_rejected(string? value)
        => Assert.Throws<ArgumentException>(() => new WorkflowRunId(value!));

    [Fact]
    public void Values_are_exact_and_default_is_empty()
    {
        Assert.True(default(WorkflowRunId).IsEmpty);
        Assert.Equal("", default(WorkflowRunId).ToString());
        Assert.Equal(new WorkflowRunId("run"), new WorkflowRunId("run"));
        Assert.NotEqual(new WorkflowRunId("run"), new WorkflowRunId("RUN"));
        Assert.Equal(" run ", new WorkflowRunId(" run ").Value);
    }
}
