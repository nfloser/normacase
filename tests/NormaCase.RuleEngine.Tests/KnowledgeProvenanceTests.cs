using System.Text.Json;
using System.Text.Json.Nodes;
using NormaCase.Domain.Cases;
using NormaCase.Domain.Decision;
using NormaCase.Knowledge.Serialization;
using NormaCase.Knowledge.Validation;
using NormaCase.RuleEngine.Evaluation;
using Xunit;

namespace NormaCase.RuleEngine.Tests;

public sealed class KnowledgeProvenanceTests
{
    private static readonly DateOnly AssessmentDate = new(2026, 10, 2);

    [Fact]
    public void Missing_format_version_is_rejected()
    {
        var node = DemoNode();
        node["manifest"]!.AsObject().Remove("formatVersion");

        var error = Assert.Throws<KnowledgeValidationException>(
            () => new KnowledgePackLoader().LoadFromJson(node.ToJsonString()));

        Assert.Contains(
            error.Errors,
            item => item.Code == "unsupported_format_version");
    }

    [Fact]
    public void Unsupported_format_version_is_rejected()
    {
        var node = DemoNode();
        node["manifest"]!["formatVersion"] = 999;

        var error = Assert.Throws<KnowledgeValidationException>(
            () => new KnowledgePackLoader().LoadFromJson(node.ToJsonString()));

        Assert.Contains(
            error.Errors,
            item => item.Code == "unsupported_format_version");
    }

    [Fact]
    public void Invalid_source_validity_interval_is_rejected()
    {
        var node = DemoNode();
        var source = node["sources"]![0]!;
        source["validFrom"] = "2026-06-01";
        source["validUntil"] = "2026-05-31";

        var error = Assert.Throws<KnowledgeValidationException>(
            () => new KnowledgePackLoader().LoadFromJson(node.ToJsonString()));

        Assert.Contains(
            error.Errors,
            item => item.Code == "invalid_source_validity_interval");
    }

    [Fact]
    public void Public_reference_requires_retrieval_date_and_content_hash()
    {
        var node = DemoNode();
        node["manifest"]!["validationLevel"] = "PUBLIC_REFERENCE";
        var source = node["sources"]![0]!.AsObject();
        source.Remove("retrievedOn");
        source.Remove("contentHash");

        var error = Assert.Throws<KnowledgeValidationException>(
            () => new KnowledgePackLoader().LoadFromJson(node.ToJsonString()));

        Assert.Contains(
            error.Errors,
            item => item.Code == "missing_source_retrieval_date");
        Assert.Contains(
            error.Errors,
            item => item.Code == "missing_source_content_hash");
    }

    [Fact]
    public void Malformed_sha256_content_hash_is_rejected()
    {
        var node = DemoNode();
        node["sources"]![0]!["contentHash"] = "sha256:not-a-real-hash";

        var error = Assert.Throws<KnowledgeValidationException>(
            () => new KnowledgePackLoader().LoadFromJson(node.ToJsonString()));

        Assert.Contains(
            error.Errors,
            item => item.Code == "invalid_source_content_hash");
    }

    [Fact]
    public void Decision_trace_carries_source_revision_metadata()
    {
        var pack = new KnowledgePackLoader().LoadFromJson(DemoNode().ToJsonString());
        var facts = new Dictionary<string, CaseValue>
        {
            ["criterion_a"] = TruthValue.Yes,
            ["criterion_b"] = TruthValue.Yes,
            ["criterion_c"] = TruthValue.No
        };

        var result = new RuleEvaluator().Evaluate(
            pack,
            facts,
            AssessmentDate);

        var serialized = JsonSerializer.Serialize(result.RuleTrace);

        Assert.Contains("\"SourceVersion\":\"synthetic-v1\"", serialized, StringComparison.Ordinal);
        Assert.Contains("\"SourceLocation\":\"synthetic://demo-a/source-001\"", serialized, StringComparison.Ordinal);
    }

    private static JsonNode DemoNode()
    {
        var node = JsonNode.Parse(
            File.ReadAllText(
                Path.Combine(
                    AppContext.BaseDirectory,
                    "Fixtures",
                    "demo-a-pack.json")))!;

        node["manifest"]!["formatVersion"] = 1;

        var source = node["sources"]![0]!;
        source["version"] = "synthetic-v1";
        source["location"] = "synthetic://demo-a/source-001";
        source["publishedOn"] = "2026-01-01";
        source["validFrom"] = "2026-01-01";

        return node;
    }
}
