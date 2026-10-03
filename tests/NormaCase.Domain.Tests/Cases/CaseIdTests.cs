using NormaCase.Domain.Cases;
using Xunit;

namespace NormaCase.Domain.Tests.Cases;

public sealed class CaseIdTests
{
    [Fact]
    public void Explicit_value_is_preserved_and_compares_by_value()
    {
        var first = new CaseId("case-synthetic-001");
        var second = new CaseId("case-synthetic-001");

        Assert.Equal("case-synthetic-001", first.Value);
        Assert.Equal(first, second);
        Assert.Equal("case-synthetic-001", first.ToString());
        Assert.False(first.IsEmpty);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public void Blank_values_are_rejected(string value)
        => Assert.Throws<ArgumentException>(
            () => new CaseId(value));

    [Fact]
    public void Default_value_is_explicitly_empty()
    {
        CaseId value = default;

        Assert.True(value.IsEmpty);
        Assert.Equal(string.Empty, value.ToString());
    }
}
