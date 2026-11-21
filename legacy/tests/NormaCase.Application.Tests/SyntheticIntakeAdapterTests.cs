using System.Text.Json;
using System.Xml;
using NormaCase.Domain.Cases;
using NormaCase.Domain.Decision;
using NormaCase.SyntheticIntegration;
using Xunit;

namespace NormaCase.Application.Tests;

public sealed class SyntheticIntakeAdapterTests
{
    private static readonly DateTimeOffset Time = new(2026, 10, 3, 12, 0, 0, TimeSpan.Zero);
    private const string Prefix = "<SyntheticCase formatVersion=\"1\" order=\"order\" message=\"message\" revision=\"1\" date=\"2026-10-03\">";
    [Fact]
    public void Xml_preserves_exact_decimal_and_unknown_without_external_resolution()
    {
        var request = SyntheticIntakeAdapters.Xml(Prefix + "<Fact id=\"measurement\" kind=\"NUMBER\" value=\"15.1234567890123456789\"/><Fact id=\"confirmed\" kind=\"TRUTH\" value=\"UNKNOWN\"/></SyntheticCase>", Time);
        Assert.Equal(CaseValue.FromNumber(15.1234567890123456789m), request.Facts["measurement"]);
        Assert.True(request.Facts["confirmed"].IsUnknown);
        Assert.Equal(Time, request.Provenance.ReceivedAtUtc);
        Assert.Equal("synthetic-xml", request.Provenance.SourceSystemId);
    }
    [Theory]
    [InlineData("<Fact id=\"number\" kind=\"NUMBER\" value=\"0.12345678901234567890123456789\"/>")]
    [InlineData("<Fact id=\"truth\" kind=\"TRUTH\" value=\"MAYBE\"/>")]
    [InlineData("<Fact id=\"truth\" kind=\"TRUTH\" value=\"YES\"/><Fact id=\"truth\" kind=\"TRUTH\" value=\"NO\"/>")]
    [InlineData("<Other/>")]
    [InlineData("<Fact id=\"truth\" kind=\"TRUTH\" value=\"YES\"><Other/></Fact>")]
    public void Xml_rejects_ambiguous_or_unrepresentable_values(string body)
        => Assert.ThrowsAny<Exception>(() => SyntheticIntakeAdapters.Xml(Prefix + body + "</SyntheticCase>", Time));
    [Fact]
    public void Xml_prohibits_dtd_and_external_entities()
        => Assert.Throws<XmlException>(() => SyntheticIntakeAdapters.Xml("<!DOCTYPE x [<!ENTITY ext SYSTEM 'file:///etc/passwd'>]><SyntheticCase/>", Time));
    [Theory]
    [InlineData("\"1\"")]
    [InlineData("\"01\"")]
    [InlineData("1")]
    public void Json_requires_a_strict_envelope_and_canonical_revision(string revision)
    {
        var text = "{\"formatVersion\":1,\"order\":\"order\",\"message\":\"message\",\"revision\":" + revision + ",\"input\":{\"formatVersion\":1,\"assessmentDate\":\"2026-10-03\",\"facts\":{\"confirmed\":{\"kind\":\"UNKNOWN\"}},\"evidence\":{}}}";
        if (revision == "\"1\"") Assert.True(SyntheticIntakeAdapters.Json(text, Time).Facts["confirmed"].IsUnknown);
        else Assert.ThrowsAny<Exception>(() => SyntheticIntakeAdapters.Json(text, Time));
        Assert.Throws<FormatException>(() => SyntheticIntakeAdapters.Json(text.Replace("\"order\":\"order\"", "\"order\":\"order\",\"order\":\"second\""), Time));
    }
}
