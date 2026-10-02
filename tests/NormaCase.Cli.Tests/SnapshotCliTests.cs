using System.Text.Json.Nodes;
using NormaCase.Replay;
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


    [Fact]
    public void Non_synthetic_pack_is_rejected_for_capture_and_replay()
    {
        var pack = JsonNode.Parse(File.ReadAllText(Fixture("demo-a")))!;
        pack["manifest"]!["validationLevel"] = "PUBLIC_REFERENCE";
        pack["sources"]![0]!["retrievedAt"] = "2026-10-02";
        pack["sources"]![0]!["contentHash"] = "sha256:" + new string('0', 64);
        var casePath = Path.Combine(AppContext.BaseDirectory, "Cases", "demo-a-supported.json");
        var packPath = Path.GetTempFileName();
        var snapshotPath = Path.GetTempFileName();
        try
        {
            File.WriteAllText(packPath, pack.ToJsonString());
            var snapshot = new AssessmentSnapshotService().Capture(pack.ToJsonString(),
                File.ReadAllText(casePath), "test-1");
            File.WriteAllText(snapshotPath, snapshot);
            using var output = new StringWriter();
            using var error = new StringWriter();
            Assert.Equal(2, CliRunner.Run(new[] { "snapshot", "--pack", packPath, "--case", casePath,
                "--platform-version", "test-1" }, output, error));
            Assert.Equal(2, CliRunner.Run(new[] { "replay", "--snapshot", snapshotPath,
                "--platform-version", "test-1" }, output, error));
            Assert.Equal("", output.ToString());
            Assert.DoesNotContain(packPath, error.ToString());
            Assert.DoesNotContain(snapshotPath, error.ToString());
        }
        finally { File.Delete(packPath); File.Delete(snapshotPath); }
    }

    [Fact]
    public void Replay_missing_file_has_private_IO_error()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Assert.Equal(3, CliRunner.Run(new[] { "replay", "--snapshot", path,
            "--platform-version", "test-1" }, output, error));
        Assert.Equal("", output.ToString());
        Assert.DoesNotContain(path, error.ToString());
    }

    private static string Fixture(string demo) => Path.Combine(AppContext.BaseDirectory, "Fixtures", demo + "-pack.json");
}
