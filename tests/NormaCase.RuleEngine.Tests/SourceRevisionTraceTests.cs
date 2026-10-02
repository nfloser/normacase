using System.Text.Json;
using System.Text.Json.Nodes;
using NormaCase.Domain.Cases;
using NormaCase.Domain.Decision;
using NormaCase.Knowledge.Serialization;
using NormaCase.Knowledge.Validation;
using NormaCase.RuleEngine.Evaluation;
using Xunit;

namespace NormaCase.RuleEngine.Tests;

public sealed class SourceRevisionTraceTests
{
    [Theory]
    [InlineData("demo-a", "criterion_a", "criterion_b")]
    [InlineData("demo-b", "score", "band_value")]
    [InlineData("demo-c", "request_confirmed", "measurement")]
    public void Trace_captures_the_selected_source_revision(string demo, string first, string second)
    {
        var pack = new KnowledgePackLoader().LoadFromFile(Fixture(demo));
        var facts = demo == "demo-b"
            ? new Dictionary<string, CaseValue> { [first] = 15m, [second] = 30m }
            : new Dictionary<string, CaseValue> { [first] = TruthValue.Yes, [second] = demo == "demo-c" ? CaseValue.FromNumber(15m) : CaseValue.FromTruth(TruthValue.Yes) };
        var result = new RuleEvaluator().Evaluate(pack, facts, new DateOnly(2026, 10, 2));
        var source = Assert.IsType<SourceTrace>(result.RuleTrace!.Source);
        var definition = Assert.Single(pack.Sources);
        Assert.Equal(definition.Id, source.Id);
        Assert.Equal(definition.Version, source.Version);
        Assert.Equal(definition.SourceLocation, source.SourceLocation);
        Assert.Equal(definition.Authority, source.Authority);
        Assert.Equal(definition.Title, source.Title);
        Assert.Equal(definition.DocumentType, source.DocumentType);
        Assert.Equal(definition.Status, source.Status);
        Assert.Equal(result.RuleTrace.SourceId, source.Id);
        Assert.Null(source.ContentHash);
        Assert.Null(source.RetrievedAt);
    }

    [Theory]
    [InlineData("version")]
    [InlineData("sourceLocation")]
    public void Every_source_requires_a_version_and_location(string property)
    {
        var node = DemoA();
        node["sources"]![0]!.AsObject().Remove(property);
        var error = Assert.Throws<KnowledgeValidationException>(() =>
            new KnowledgePackLoader().LoadFromJson(node.ToJsonString()));
        Assert.Contains(error.Errors, item => item.Code == "required_value_missing" && item.Path.EndsWith("." + property, StringComparison.Ordinal));
    }

    [Fact]
    public void Missing_format_version_is_rejected()
    {
        var node = DemoA();
        node["manifest"]!.AsObject().Remove("formatVersion");
        var error = Assert.Throws<KnowledgeValidationException>(() =>
            new KnowledgePackLoader().LoadFromJson(node.ToJsonString()));
        Assert.Contains(error.Errors, item => item.Code == "unsupported_format_version");
    }

    [Fact]
    public void Full_provenance_survives_serialization_and_later_pack_replacement()
    {
        var node = DemoA();
        var source = node["sources"]![0]!;
        source["version"] = "revision-1";
        source["publicationDate"] = "2025-12-01";
        source["validFrom"] = "2026-01-01";
        source["validUntil"] = "2026-12-31";
        source["retrievedAt"] = "2026-10-02";
        source["contentHash"] = "sha256:" + new string('a', 64);
        var loader = new KnowledgePackLoader();
        var pack = loader.LoadFromJson(node.ToJsonString());
        var facts = new Dictionary<string, CaseValue> { ["criterion_a"] = TruthValue.Yes, ["criterion_b"] = TruthValue.Yes };
        var evaluator = new RuleEvaluator();
        var historical = evaluator.Evaluate(pack, facts, new DateOnly(2026, 10, 2));
        var saved = JsonSerializer.Serialize(historical);
        pack.Sources.Clear();
        Assert.Equal(saved, JsonSerializer.Serialize(historical));
        var restored = JsonSerializer.Deserialize<AssessmentResult>(saved)!;
        Assert.Equal(historical.RuleTrace!.Source, restored.RuleTrace!.Source);
        Assert.Equal(new DateOnly(2025, 12, 1), restored.RuleTrace.Source.PublicationDate);
        Assert.Equal(new DateOnly(2026, 1, 1), restored.RuleTrace.Source.ValidFrom);
        Assert.Equal(new DateOnly(2026, 12, 31), restored.RuleTrace.Source.ValidUntil);
        Assert.Equal(new DateOnly(2026, 10, 2), restored.RuleTrace.Source.RetrievedAt);
        Assert.Equal("sha256:" + new string('a', 64), restored.RuleTrace.Source.ContentHash);

        source["version"] = "revision-2";
        source["contentHash"] = "sha256:" + new string('b', 64);
        node["manifest"]!["releaseId"] = "demo-a-next";
        var later = evaluator.Evaluate(loader.LoadFromJson(node.ToJsonString()), facts, new DateOnly(2026, 10, 2));
        Assert.NotEqual(historical.RuleTrace.Source, later.RuleTrace!.Source);
        Assert.Equal("revision-1", historical.RuleTrace.Source.Version);
        Assert.Equal("demo-a-2026.1", historical.KnowledgeRelease);
    }

    private static JsonNode DemoA() => JsonNode.Parse(File.ReadAllText(Fixture("demo-a")))!;
    private static string Fixture(string demo) => Path.Combine(AppContext.BaseDirectory, "Fixtures", demo + "-pack.json");
}
