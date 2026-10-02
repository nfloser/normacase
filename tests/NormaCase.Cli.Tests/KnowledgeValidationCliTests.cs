using System.Text.Json.Nodes;
using NormaCase.Cli;
using Xunit;

namespace NormaCase.Cli.Tests;

public sealed class KnowledgeValidationCliTests
{
    [Theory]
    [InlineData("demo-a")]
    [InlineData("demo-b")]
    [InlineData("demo-c")]
    [InlineData("demo-d")]
    [InlineData("demo-e")]
    public void Local_packs_can_be_validated_without_case_date_or_platform(string demo)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        Assert.Equal(0, CliRunner.Run(["validate", "--pack", PackPath(demo)], output, error));
        Assert.Contains("Strukturprüfung abgeschlossen", output.ToString());
        Assert.Contains("keine fachliche Freigabe", output.ToString());
        Assert.Equal("", error.ToString());
    }

    [Theory]
    [InlineData("PUBLIC_REFERENCE")]
    [InlineData("DOMAIN_REVIEWED")]
    [InlineData("PRODUCTION_APPROVED")]
    public void Metadata_inspection_does_not_authorize_non_synthetic_evaluation(string level)
    {
        var node = JsonNode.Parse(File.ReadAllText(PackPath("demo-a")))!;
        node["manifest"]!["validationLevel"] = level;
        foreach (var source in node["sources"]!.AsArray())
        {
            source!["version"] = "synthetic-revision";
            source["sourceLocation"] = "synthetic-source-only";
            source["retrievedAt"] = "2026-10-02";
            source["contentHash"] = "sha256:" + new string('0', 64);
        }
        WithPack(node.ToJsonString(), path =>
        {
            using var output = new StringWriter();
            using var error = new StringWriter();
            Assert.Equal(0, CliRunner.Run(["validate", "--pack", path], output, error));
            Assert.Contains("keine fachliche Freigabe", output.ToString());
            Assert.DoesNotContain(level, output.ToString());
            output.GetStringBuilder().Clear();
            Assert.Equal(2, CliRunner.Run(["evaluate", "--pack", path, "--case",
                Path.Combine(AppContext.BaseDirectory, "Cases", "demo-a-supported.json"),
                "--platform-version", "validation-test"], output, error));
            Assert.Equal("", output.ToString());
        });
    }

    [Theory]
    [InlineData(new[] { "validate" })]
    [InlineData(new[] { "validate", "--pack" })]
    [InlineData(new[] { "validate", "--pack", "--case" })]
    [InlineData(new[] { "validate", "--pack", "unused", "--json" })]
    [InlineData(new[] { "validate", "--pack", "unused", "--case", "unused" })]
    [InlineData(new[] { "validate", "--pack", "unused", "--platform-version", "unused" })]
    [InlineData(new[] { "validate", "--pack", "unused", "--pack", "unused" })]
    public void Validation_rejects_unrelated_or_ambiguous_arguments(string[] args)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        Assert.Equal(2, CliRunner.Run(args, output, error));
        Assert.Equal("", output.ToString());
    }

    [Fact]
    public void Semantic_errors_show_stable_codes_without_imported_messages()
    {
        var node = JsonNode.Parse(File.ReadAllText(PackPath("demo-a")))!;
        node["rules"]![0]!["sourceId"] = "private-synthetic-marker";
        WithPack(node.ToJsonString(), path =>
        {
            using var output = new StringWriter();
            using var error = new StringWriter();
            Assert.Equal(2, CliRunner.Run(["validate", "--pack", path], output, error));
            Assert.Contains("missing_source", error.ToString());
            Assert.DoesNotContain("private-synthetic-marker", error.ToString());
            Assert.DoesNotContain(path, error.ToString());
            Assert.Equal("", output.ToString());
        });
    }

    [Fact]
    public void Quoted_numeric_version_is_rejected_by_the_validation_command()
    {
        var node = JsonNode.Parse(File.ReadAllText(PackPath("demo-a")))!;
        node["manifest"]!["formatVersion"] = "1";
        WithPack(node.ToJsonString(), path =>
        {
            using var output = new StringWriter();
            using var error = new StringWriter();
            Assert.Equal(2, CliRunner.Run(["validate", "--pack", path], output, error));
            Assert.Equal("", output.ToString());
            Assert.Contains("ungültig", error.ToString());
        });
    }

    [Fact]
    public void Missing_pack_has_the_existing_private_IO_error()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + "-synthetic.json");
        Assert.Equal(3, CliRunner.Run(["validate", "--pack", path], output, error));
        Assert.Equal("", output.ToString());
        Assert.DoesNotContain(path, error.ToString());
    }

    [Fact]
    public void Help_documents_standalone_validation()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        Assert.Equal(0, CliRunner.Run(["--help"], output, error));
        Assert.Contains("validate --pack DATEI", output.ToString());
    }

    private static string PackPath(string demo) => Path.Combine(
        AppContext.BaseDirectory, "Fixtures", demo + "-pack.json");

    private static void WithPack(string json, Action<string> check)
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "synthetic-pack.json");
        try
        {
            File.WriteAllText(path, json);
            check(path);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }
}
