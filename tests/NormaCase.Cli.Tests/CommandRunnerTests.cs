using System.Text.Json;
using NormaCase.Cli;
using Xunit;

namespace NormaCase.Cli.Tests;

public sealed class CommandRunnerTests
{
    [Fact]
    public async Task Valid_synthetic_case_runs_offline_with_german_default_output()
    {
        var output = new StringWriter();
        var error = new StringWriter();

        var exitCode = await CommandRunner.RunAsync(
            [
                "evaluate",
                "--pack", Fixture("demo-a-pack.json"),
                "--case", Fixture("demo-a-supported-case.json"),
                "--date", "2026-10-02"
            ],
            output,
            error);

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Contains("Ergebnis: Unterstützt", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("Knowledge Release: demo-a-2026.1", output.ToString(), StringComparison.Ordinal);
        Assert.Equal(string.Empty, error.ToString());
    }

    [Fact]
    public async Task Output_option_writes_versioned_assessment_json()
    {
        var target = Path.Combine(Path.GetTempPath(), $"normacase-{Guid.NewGuid():N}.json");
        try
        {
            var exitCode = await CommandRunner.RunAsync(
                [
                    "evaluate",
                    "--pack", Fixture("demo-a-pack.json"),
                    "--case", Fixture("demo-a-supported-case.json"),
                    "--date", "2026-10-02",
                    "--output", target,
                    "--platform-version", "0.1.0-test"
                ],
                TextWriter.Null,
                TextWriter.Null);

            Assert.Equal(ExitCodes.Success, exitCode);
            using var document = JsonDocument.Parse(await File.ReadAllTextAsync(target));
            Assert.Equal(1, document.RootElement.GetProperty("formatVersion").GetInt32());
            Assert.Equal("0.1.0-test", document.RootElement.GetProperty("platformVersion").GetString());
            Assert.Equal("SUPPORTED", document.RootElement.GetProperty("assessment").GetProperty("outcome").GetString());
        }
        finally
        {
            if (File.Exists(target))
                File.Delete(target);
        }
    }

    [Fact]
    public async Task Unknown_case_properties_fail_closed_without_echoing_case_content()
    {
        var output = new StringWriter();
        var error = new StringWriter();

        var exitCode = await CommandRunner.RunAsync(
            [
                "evaluate",
                "--pack", Fixture("demo-a-pack.json"),
                "--case", Fixture("demo-a-invalid-extra-property.json"),
                "--date", "2026-10-02"
            ],
            output,
            error);

        Assert.Equal(ExitCodes.InputOrKnowledgeFailure, exitCode);
        Assert.Contains("Eingabedatei ist ungültig", error.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("Synthetic Secret", error.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("Synthetic Secret", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Missing_files_use_distinct_io_exit_code()
    {
        var error = new StringWriter();

        var exitCode = await CommandRunner.RunAsync(
            [
                "evaluate",
                "--pack", Fixture("missing-pack.json"),
                "--case", Fixture("demo-a-supported-case.json"),
                "--date", "2026-10-02"
            ],
            TextWriter.Null,
            error);

        Assert.Equal(ExitCodes.IoFailure, exitCode);
        Assert.Contains("Datei konnte nicht gelesen oder geschrieben werden", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Help_is_available_in_german()
    {
        var output = new StringWriter();

        var exitCode = await CommandRunner.RunAsync(["--help"], output, TextWriter.Null);

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Contains("NormaCase Offline-Auswertung", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("--date", output.ToString(), StringComparison.Ordinal);
    }

    private static string Fixture(string name)
        => Path.Combine(AppContext.BaseDirectory, "Fixtures", name);
}
