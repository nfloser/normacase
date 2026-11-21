using NormaCase.Domain.Cases;
using Xunit;

namespace NormaCase.Domain.Tests;

public sealed class CaseIdTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Blank_identity_is_rejected(string? value)
        => Assert.Throws<ArgumentException>(() => new CaseId(value!));

    [Fact]
    public void Identity_is_exact_and_default_is_empty()
    {
        Assert.True(default(CaseId).IsEmpty);
        Assert.Equal(string.Empty, default(CaseId).ToString());
        Assert.Equal(new CaseId("case-001"), new CaseId("case-001"));
        Assert.NotEqual(new CaseId("case-001"), new CaseId("CASE-001"));
        Assert.Equal(" case-001 ", new CaseId(" case-001 ").Value);
    }
}
