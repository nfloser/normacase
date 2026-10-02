using System.Text.Json;
using System.Text.Json.Nodes;
using NormaCase.Cli;
using NormaCase.Domain.Decision;
using NormaCase.Serialization;
using Xunit;

namespace NormaCase.Cli.Tests;

public sealed class CliRunnerTests
{
    [Theory]
    [InlineData("demo-a-supported", "demo-a", AssessmentOutcome.Supported)]
    [InlineData("demo-a-incomplete", "demo-a", AssessmentOutcome.Incomplete)]
    [InlineData("demo-b-supported", "demo-b", AssessmentOutcome.Supported)]
    [InlineData("demo-c-supported", "demo-c", AssessmentOutcome.Supported)]
    [InlineData("demo-c-review", "demo-c", AssessmentOutcome.HumanReview)]
    public void Real_files_evaluate_and_export_versioned_results(string name, string demo, AssessmentOutcome expected)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var code = CliRunner.Run(Args(demo, CasePath(name), json: true), output, error);
        Assert.Equal(0, code);
        Assert.Equal("", error.ToString());
        var result = AssessmentJson.Deserialize(output.ToString());
        Assert.Equal(expected, result.Assessment.Outcome);
        Assert.Equal("test-platform-1", result.PlatformVersion);
        Assert.Equal(new DateOnly(2026, 10, 2), result.Assessment.AssessmentDate);
        Assert.Equal(demo + "-2026.1", result.Assessment.KnowledgeRelease);
    }

    [Fact]
    public void Human_output_is_German_and_uses_German_dates()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        Assert.Equal(0, CliRunner.Run(Args("demo-a", CasePath("demo-a-supported")), output, error));
        Assert.Contains("Voraussetzungen erfüllt", output.ToString());
        Assert.Contains("Prüfdatum: 02.10.2026", output.ToString());
        Assert.DoesNotContain("SUPPORTED", output.ToString());
    }

    [Fact]
    public void Help_is_German_and_does_not_require_files()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        Assert.Equal(0, CliRunner.Run(["--help"], output, error));
        Assert.Contains("synthetischer Fälle", output.ToString());
        Assert.Equal("", error.ToString());
    }

    [Fact]
    public void Missing_date_is_rejected_without_inventing_today()
    {
        var node = JsonNode.Parse(File.ReadAllText(CasePath("demo-a-supported")))!;
        node.AsObject().Remove("assessmentDate");
        AssertRejected(node.ToJsonString());
    }

    [Theory]
    [InlineData("{\"formatVersion\":1,\"formatVersion\":1,\"assessmentDate\":\"2026-10-02\",\"facts\":{}}")]
    [InlineData("{\"formatVersion\":1,\"assessmentDate\":\"2026-10-02\",\"facts\":{},\"unexpected\":true}")]
    [InlineData("{\"formatVersion\":1,\"assessmentDate\":\"2026-10-02\",\"facts\":null}")]
    [InlineData("{\"formatVersion\":2,\"assessmentDate\":\"2026-10-02\",\"facts\":{}}")]
    [InlineData("{\"formatVersion\":1,\"assessmentDate\":\"2026-10-02\",\"facts\":{\"criterion_a\":{\"kind\":\"NUMBER\",\"number\":1}}}")]
    [InlineData("{\"formatVersion\":1,\"assessmentDate\":\"2026-10-02\",\"facts\":{},\"evidence\":{\"verification\":1}}")]
    public void Malformed_or_mismatched_input_is_rejected(string json) => AssertRejected(json);

    [Fact]
    public void Error_output_does_not_expose_undeclared_field_names_or_values()
        => AssertRejected("{\"formatVersion\":1,\"assessmentDate\":\"2026-10-02\",\"facts\":{\"secret_synthetic_marker\":{\"kind\":\"NUMBER\",\"number\":98765}}}");

    [Fact]
    public void Missing_file_has_a_separate_IO_code_without_printing_paths()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + "-secret.json");
        Assert.Equal(3, CliRunner.Run(Args("demo-a", path), output, error));
        Assert.Contains("Datei konnte nicht gelesen", error.ToString());
        Assert.DoesNotContain(path, error.ToString());
        Assert.Equal("", output.ToString());
    }

    [Fact]
    public void Duplicate_arguments_are_rejected_before_evaluation()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var args = Args("demo-a", CasePath("demo-a-supported")).Concat(new[] { "--json", "--json" }).ToArray();
        Assert.Equal(2, CliRunner.Run(args, output, error));
        Assert.Equal("", output.ToString());
    }

    [Fact]
    public void Runtime_can_be_repeated_without_changing_export()
    {
        using var first = new StringWriter();
        using var second = new StringWriter();
        using var error = new StringWriter();
        var args = Args("demo-b", CasePath("demo-b-supported"), json: true);
        Assert.Equal(0, CliRunner.Run(args, first, error));
        Assert.Equal(0, CliRunner.Run(args, second, error));
        Assert.Equal(first.ToString(), second.ToString());
    }

    [Theory]
    [InlineData("manifest")]
    [InlineData("fields")]
    [InlineData("sources")]
    [InlineData("rules")]
    public void Null_required_pack_sections_fail_as_input_errors(string property)
    {
        var node = JsonNode.Parse(File.ReadAllText(PackPath("demo-a")))!;
        node[property] = null;
        AssertPackRejected(node.ToJsonString());
    }

    [Fact]
    public void Null_nested_pack_entries_are_rejected()
    {
        var node = JsonNode.Parse(File.ReadAllText(PackPath("demo-a")))!;
        node["rules"]![0]!["condition"]!["conditions"]!.AsArray().Add((JsonNode?)null);
        AssertPackRejected(node.ToJsonString());
    }

    [Fact]
    public void Unknown_pack_members_and_numeric_outcomes_are_rejected()
    {
        var node = JsonNode.Parse(File.ReadAllText(PackPath("demo-a")))!;
        node["secret_synthetic_marker"] = true;
        AssertPackRejected(node.ToJsonString());
        node.AsObject().Remove("secret_synthetic_marker");
        node["rules"]![0]!["onMatch"] = 0;
        AssertPackRejected(node.ToJsonString());
    }

    [Fact]
    public void Case_insensitive_duplicate_pack_properties_are_rejected()
    {
        var json = File.ReadAllText(PackPath("demo-a"));
        AssertPackRejected(json.Replace("\"formatVersion\": 1", "\"formatVersion\": 1, \"FormatVersion\": 1"));
    }

    private static void AssertPackRejected(string json)
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, json);
            using var output = new StringWriter();
            using var error = new StringWriter();
            var args = Args("demo-a", CasePath("demo-a-supported"));
            args[2] = path;
            Assert.Equal(2, CliRunner.Run(args, output, error));
            Assert.Equal("", output.ToString());
            Assert.Contains("ungültig", error.ToString());
            Assert.DoesNotContain("secret_synthetic_marker", error.ToString());
            Assert.DoesNotContain(path, error.ToString());
        }
        finally { File.Delete(path); }
    }

    private static string PackPath(string demo) => Path.Combine(AppContext.BaseDirectory, "Fixtures", demo + "-pack.json");

    private static void AssertRejected(string json)
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, json);
            using var output = new StringWriter();
            using var error = new StringWriter();
            Assert.Equal(2, CliRunner.Run(Args("demo-a", path), output, error));
            Assert.Equal("", output.ToString());
            Assert.Contains("ungültig", error.ToString());
            Assert.DoesNotContain("secret_synthetic_marker", error.ToString());
            Assert.DoesNotContain("98765", error.ToString());
        }
        finally { File.Delete(path); }
    }

    private static string[] Args(string demo, string path, bool json = false)
    {
        var args = new List<string> { "evaluate", "--pack", Path.Combine(AppContext.BaseDirectory, "Fixtures", demo + "-pack.json"), "--case", path, "--platform-version", "test-platform-1" };
        if (json) args.Add("--json");
        return args.ToArray();
    }

    private static string CasePath(string name) => Path.Combine(AppContext.BaseDirectory, "Cases", name + ".json");
}
