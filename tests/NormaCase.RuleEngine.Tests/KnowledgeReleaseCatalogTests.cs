using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using NormaCase.Knowledge.Catalog;
using NormaCase.Knowledge.Validation;
using Xunit;

namespace NormaCase.RuleEngine.Tests;

public sealed class KnowledgeReleaseCatalogTests
{
    [Fact]
    public void Multiple_releases_of_one_pack_are_selected_only_by_exact_identity()
    {
        var catalog = new KnowledgeReleaseCatalog();
        var firstJson = DemoAJson();
        var secondJson = Release(firstJson, "demo-a-2026.2");

        var first = catalog.Register(firstJson);
        var second = catalog.Register(secondJson);

        Assert.Equal("demo-a-2026.1", first.ReleaseId);
        Assert.Equal("demo-a-2026.2", second.ReleaseId);
        Assert.Same(
            first,
            catalog.Get("synthetic.demo-a", "demo-a-2026.1"));
        Assert.Same(
            second,
            catalog.Get("synthetic.demo-a", "demo-a-2026.2"));
        Assert.Throws<KnowledgeReleaseNotFoundException>(
            () => catalog.Get("synthetic.demo-a", "latest"));
    }

    [Fact]
    public void Registration_fingerprints_the_exact_utf8_json_bytes()
    {
        var json = DemoAJson();
        var artifact = new KnowledgeReleaseCatalog().Register(json);

        var expected = Convert.ToHexStringLower(
            SHA256.HashData(Encoding.UTF8.GetBytes(json)));

        Assert.Equal(expected, artifact.Sha256);
        Assert.Equal("synthetic.demo-a", artifact.PackId);
        Assert.Equal("demo-a-2026.1", artifact.ReleaseId);
        Assert.Equal("ACTIVE", artifact.LifecycleStatus);
        Assert.Equal("SYNTHETIC", artifact.ValidationLevel);
    }

    [Fact]
    public void Loading_returns_a_fresh_validated_pack_each_time()
    {
        var artifact = new KnowledgeReleaseCatalog().Register(
            DemoAJson());

        var first = artifact.LoadPack();
        first.Fields.Clear();
        first.Sources.Clear();
        first.Rules.Clear();

        var second = artifact.LoadPack();

        Assert.Equal("synthetic.demo-a", second.Manifest.PackId);
        Assert.Equal("demo-a-2026.1", second.Manifest.ReleaseId);
        Assert.Equal(3, second.Fields.Count);
        Assert.Single(second.Sources);
    }

    [Fact]
    public void Identical_duplicate_registration_is_idempotent()
    {
        var catalog = new KnowledgeReleaseCatalog();
        var json = DemoAJson();

        var first = catalog.Register(json);
        var second = catalog.Register(json);

        Assert.Same(first, second);
        Assert.Single(catalog.Releases);
    }

    [Fact]
    public void Same_release_identity_with_different_bytes_is_rejected()
    {
        var catalog = new KnowledgeReleaseCatalog();
        var original = DemoAJson();
        var changed = JsonNode.Parse(original)!;
        changed["manifest"]!["lifecycleStatus"] = "DEPRECATED";

        catalog.Register(original);

        var exception =
            Assert.Throws<KnowledgeReleaseIdentityConflictException>(
                () => catalog.Register(changed.ToJsonString()));

        Assert.Equal("synthetic.demo-a", exception.PackId);
        Assert.Equal("demo-a-2026.1", exception.ReleaseId);
    }

    [Fact]
    public void Same_release_identity_with_only_byte_formatting_changes_is_still_a_conflict()
    {
        var catalog = new KnowledgeReleaseCatalog();
        var original = DemoAJson();

        catalog.Register(original);

        Assert.Throws<KnowledgeReleaseIdentityConflictException>(
            () => catalog.Register(original + Environment.NewLine));
    }

    [Fact]
    public void Invalid_pack_is_rejected_before_it_can_enter_the_catalog()
    {
        var invalid = JsonNode.Parse(DemoAJson())!;
        invalid["manifest"]!["entryRuleId"] = "missing-rule";
        var catalog = new KnowledgeReleaseCatalog();

        Assert.Throws<KnowledgeValidationException>(
            () => catalog.Register(invalid.ToJsonString()));

        Assert.Empty(catalog.Releases);
    }

    [Fact]
    public void Enumeration_is_deterministic_by_pack_and_release_identity()
    {
        var catalog = new KnowledgeReleaseCatalog();
        var demoA = DemoAJson();

        catalog.Register(Release(demoA, "demo-a-2026.2"));
        catalog.Register(DemoCJson());
        catalog.Register(demoA);

        Assert.Equal(
            [
                "synthetic.demo-a/demo-a-2026.1",
                "synthetic.demo-a/demo-a-2026.2",
                "synthetic.demo-c/demo-c-2026.1"
            ],
            catalog.Releases
                .Select(item => item.PackId + "/" + item.ReleaseId)
                .ToArray());
    }

    [Theory]
    [InlineData("", "demo-a-2026.1")]
    [InlineData("synthetic.demo-a", "")]
    public void Lookup_requires_explicit_non_blank_identity(
        string packId,
        string releaseId)
    {
        var catalog = new KnowledgeReleaseCatalog();

        Assert.Throws<ArgumentException>(
            () => catalog.Get(packId, releaseId));
    }

    private static string Release(
        string json,
        string releaseId)
    {
        var node = JsonNode.Parse(json)!;
        node["manifest"]!["releaseId"] = releaseId;
        return node.ToJsonString();
    }

    private static string DemoAJson()
        => File.ReadAllText(
            Path.Combine(
                AppContext.BaseDirectory,
                "Fixtures",
                "demo-a-pack.json"));

    private static string DemoCJson()
        => File.ReadAllText(
            Path.Combine(
                AppContext.BaseDirectory,
                "Fixtures",
                "demo-c-pack.json"));
}
