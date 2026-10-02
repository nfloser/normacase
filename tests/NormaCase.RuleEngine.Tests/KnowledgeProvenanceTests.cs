using System.Text.Json.Nodes;
using NormaCase.Knowledge.Serialization;
using NormaCase.Knowledge.Validation;
using Xunit;

namespace NormaCase.RuleEngine.Tests;

public sealed class KnowledgeProvenanceTests
{
    private readonly KnowledgePackLoader _loader = new();

    [Theory]
    [InlineData("demo-a-pack.json")]
    [InlineData("demo-b-pack.json")]
    [InlineData("demo-c-pack.json")]
    public void Existing_synthetic_releases_still_load(string fixture)
    {
        var pack = _loader.LoadFromFile(FixturePath(fixture));

        Assert.Equal(1, pack.Manifest.FormatVersion);
        Assert.Equal("ACTIVE", pack.Manifest.LifecycleStatus);
        Assert.Equal("SYNTHETIC", pack.Manifest.ValidationLevel);
    }

    [Fact]
    public void Unsupported_pack_format_is_rejected()
    {
        var node = DemoA();
        node["manifest"]!["formatVersion"] = 2;

        var exception = LoadFailure(node);

        Assert.Contains(
            exception.Errors,
            error => error.Code == "unsupported_format_version");
    }

    [Fact]
    public void Unknown_release_lifecycle_is_rejected()
    {
        var node = DemoA();
        node["manifest"]!["lifecycleStatus"] = "MAGIC";

        var exception = LoadFailure(node);

        Assert.Contains(
            exception.Errors,
            error => error.Code == "unknown_lifecycle_status");
    }

    [Fact]
    public void Source_validity_cannot_end_before_it_starts()
    {
        var node = DemoA();
        var source = node["sources"]![0]!;
        source["validFrom"] = "2026-10-02";
        source["validUntil"] = "2026-10-01";

        var exception = LoadFailure(node);

        Assert.Contains(
            exception.Errors,
            error => error.Code == "invalid_source_validity_interval");
    }

    [Fact]
    public void Malformed_content_hash_is_rejected_even_for_synthetic_sources()
    {
        var node = DemoA();
        node["sources"]![0]!["contentHash"] = "sha256:not-a-real-hash";

        var exception = LoadFailure(node);

        Assert.Contains(
            exception.Errors,
            error => error.Code == "invalid_source_content_hash");
    }

    [Fact]
    public void Public_reference_requires_retrievable_hashed_source_provenance()
    {
        var node = DemoA();
        node["manifest"]!["validationLevel"] = "PUBLIC_REFERENCE";

        var exception = LoadFailure(node);

        Assert.Contains(exception.Errors, error => error.Code == "missing_source_location");
        Assert.Contains(exception.Errors, error => error.Code == "missing_source_retrieved_at");
        Assert.Contains(exception.Errors, error => error.Code == "missing_source_content_hash");
    }

    [Fact]
    public void Public_reference_with_minimum_provenance_can_load_without_becoming_domain_approved()
    {
        var node = DemoA();
        node["manifest"]!["validationLevel"] = "PUBLIC_REFERENCE";

        var source = node["sources"]![0]!;
        source["sourceLocation"] = "https://example.invalid/synthetic-official-source";
        source["retrievedAt"] = "2026-10-02";
        source["contentHash"] = $"sha256:{new string('a', 64)}";

        var pack = _loader.LoadFromJson(node.ToJsonString());

        Assert.Equal("PUBLIC_REFERENCE", pack.Manifest.ValidationLevel);
        Assert.Equal("ACTIVE", pack.Manifest.LifecycleStatus);
    }

    private KnowledgeValidationException LoadFailure(JsonNode node)
        => Assert.Throws<KnowledgeValidationException>(
            () => _loader.LoadFromJson(node.ToJsonString()));

    private static JsonNode DemoA()
        => JsonNode.Parse(File.ReadAllText(FixturePath("demo-a-pack.json")))!;

    private static string FixturePath(string fileName)
        => Path.Combine(AppContext.BaseDirectory, "Fixtures", fileName);
}
