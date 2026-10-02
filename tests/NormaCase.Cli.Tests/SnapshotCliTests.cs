using NormaCase.Serialization;
using Xunit;

namespace NormaCase.Cli.Tests;

public sealed class SnapshotCliTests
{
    [Fact]
    public void Capture_and_replay_need_only_the_snapshot_and_explicit_platform()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var code = CliRunner.Run(new[] { "snapshot", "--pack", Fixture("demo-e"), "--case",
            Path.Combine(AppContext.BaseDirectory, "Cases", "demo-e-partial.json"),
            "--platform-version", "test-1" }, output, error);
        Assert.Equal(0, code);
        Assert.Equal("", error.ToString());
        var snapshot = AssessmentSnapshotJson.Deserialize(output.ToString());
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, output.ToString());
            output.GetStringBuilder().Clear();
            Assert.Equal(0, CliRunner.Run(new[] { "replay", "--snapshot", path,
                "--platform-version", "test-1", "--json" }, output, error));
            Assert.Equal(AssessmentJson.Serialize(snapshot.Assessment.Assessment, "test-1") + Environment.NewLine, output.ToString());
            output.GetStringBuilder().Clear();
            Assert.Equal(0, CliRunner.Run(new[] { "replay", "--snapshot", path,
                "--platform-version", "test-1" }, output, error));
            Assert.Contains("Offline-Wiederholung bestätigt", output.ToString());
            output.GetStringBuilder().Clear();
            Assert.Equal(4, CliRunner.Run(new[] { "replay", "--snapshot", path,
                "--platform-version", "other-secret" }, output, error));
            Assert.Equal("", output.ToString());
            Assert.DoesNotContain("other-secret", error.ToString());
            Assert.DoesNotContain(path, error.ToString());
        }
        finally { File.Delete(path); }
    }

    [Theory]
    [InlineData("snapshot", "--json")]
    [InlineData("snapshot", "--snapshot")]
    [InlineData("replay", "--pack")]
    [InlineData("replay", "--case")]
    public void Irrelevant_options_are_rejected_before_file_access(string command, string flag)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        Assert.Equal(2, CliRunner.Run(new[] { command, flag, "missing-secret-file", "--platform-version", "1" }, output, error));
        Assert.Equal("", output.ToString());
        Assert.DoesNotContain("missing-secret-file", error.ToString());
    }

    private static string Fixture(string demo) => Path.Combine(AppContext.BaseDirectory, "Fixtures", demo + "-pack.json");
}
