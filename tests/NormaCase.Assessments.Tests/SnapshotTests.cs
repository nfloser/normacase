using System.Text.Json;
using System.Text.Json.Nodes;
using NormaCase.Assessments;
using NormaCase.Serialization;
using Xunit;

namespace NormaCase.Assessments.Tests;

public sealed class SnapshotTests
{
    [Theory]
    [InlineData("demo-a-supported")]
    [InlineData("demo-a-incomplete")]
    [InlineData("demo-b-supported")]
    [InlineData("demo-c-supported")]
    [InlineData("demo-d-supported")]
    [InlineData("demo-e-partial")]
    [InlineData("demo-e-mixed")]
    [InlineData("demo-c-review")]
    public void Capture_is_deterministic_and_replay_preserves_the_complete_document(string example)
    {
        var pack = Pack(example[..6]);
        var input = Case(example);
        var service = new AssessmentSnapshotService();
        var json = service.Capture(pack, input, "test-1");
        Assert.Equal(json, service.Capture(pack, input, "test-1"));
        var snapshot = AssessmentSnapshotJson.Deserialize(json);
        Assert.Equal(pack, snapshot.KnowledgePackJson);
        var replay = service.Replay(json, "test-1");
        Assert.Equal(AssessmentJson.Serialize(snapshot.Assessment.Assessment, "test-1"),
            AssessmentJson.Serialize(replay.Assessment, "test-1"));
    }

    [Fact]
    public void Original_decimal_precision_is_preserved()
    {
        var input = JsonNode.Parse(Case("demo-b-supported"))!;
        input["facts"]!["score"] = JsonNode.Parse("{\"kind\":\"NUMBER\",\"number\":123456789.1234567890123456789}");
        var json = new AssessmentSnapshotService().Capture(Pack("demo-b"), input.ToJsonString(), "test-1");
        var snapshot = AssessmentSnapshotJson.Deserialize(json);
        Assert.Equal(123456789.1234567890123456789m, snapshot.Input.Facts["score"].Number);
        new AssessmentSnapshotService().Replay(json, "test-1");
    }

    [Theory]
    [InlineData("knowledgePackJson", "\"altered\"")]
    [InlineData("contentSha256", "\"0000000000000000000000000000000000000000000000000000000000000000\"")]
    [InlineData("formatVersion", "2")]
    [InlineData("unexpected", "true")]
    [InlineData("input", "null")]
    public void Altered_or_invalid_envelopes_are_rejected(string property, string value)
    {
        var node = JsonNode.Parse(Capture())!;
        node[property] = JsonNode.Parse(value);
        Assert.Throws<JsonException>(() => AssessmentSnapshotJson.Deserialize(node.ToJsonString()));
    }

    [Fact]
    public void Altered_unused_input_is_detected()
    {
        var node = JsonNode.Parse(Capture())!;
        node["input"]!["facts"]!["unused"] = JsonNode.Parse("{\"kind\":\"UNKNOWN\"}");
        Assert.Throws<JsonException>(() => AssessmentSnapshotJson.Deserialize(node.ToJsonString()));
    }

    [Fact]
    public void Duplicate_properties_are_rejected()
    {
        Assert.Throws<JsonException>(() => AssessmentSnapshotJson.Deserialize(
            Capture().Replace("\"formatVersion\":1", "\"formatVersion\":1,\"formatVersion\":1")));
    }

    [Fact]
    public void Replay_requires_the_recorded_platform_version()
    {
        Assert.Throws<SnapshotReplayException>(() => new AssessmentSnapshotService().Replay(Capture(), "test-2"));
    }

    [Fact]
    public void Recomputing_the_checksum_does_not_hide_a_changed_result()
    {
        var original = AssessmentSnapshotJson.Deserialize(Capture());
        var changed = original.Assessment with {
            Assessment = original.Assessment.Assessment with { MissingRequiredFields = new[] { "invented" } }
        };
        var json = AssessmentSnapshotJson.Serialize(original.KnowledgePackJson, original.Input, changed);
        Assert.Throws<SnapshotReplayException>(() => new AssessmentSnapshotService().Replay(json, "test-1"));
    }

    [Fact]
    public void Replay_uses_the_embedded_historical_pack_and_explicit_date()
    {
        var input = JsonNode.Parse(Case("demo-b-supported"))!;
        input["assessmentDate"] = "2025-01-01";
        var service = new AssessmentSnapshotService();
        var json = service.Capture(Pack("demo-b"), input.ToJsonString(), "test-1");
        Assert.Equal(new DateOnly(2025, 1, 1), service.Replay(json, "test-1").Assessment.AssessmentDate);
    }


    [Fact]
    public void Rehashed_pack_release_change_is_rejected()
    {
        var original = AssessmentSnapshotJson.Deserialize(Capture());
        var pack = JsonNode.Parse(original.KnowledgePackJson)!;
        pack["manifest"]!["releaseId"] = "synthetic-changed-release";
        var json = AssessmentSnapshotJson.Serialize(pack.ToJsonString(), original.Input, original.Assessment);
        Assert.Throws<SnapshotReplayException>(() => new AssessmentSnapshotService().Replay(json, "test-1"));
    }

    [Fact]
    public void Rehashed_trace_change_is_rejected()
    {
        var original = AssessmentSnapshotJson.Deserialize(Capture());
        var trace = original.Assessment.Assessment.RuleTrace!;
        var result = original.Assessment.Assessment with { RuleTrace = trace with { RuleVersion = "changed-version" } };
        var json = AssessmentSnapshotJson.Serialize(original.KnowledgePackJson, original.Input,
            original.Assessment with { Assessment = result });
        Assert.Throws<SnapshotReplayException>(() => new AssessmentSnapshotService().Replay(json, "test-1"));
    }

    [Theory]
    [InlineData("input", "formatVersion", "2")]
    [InlineData("assessment", "formatVersion", "1")]
    [InlineData("assessment", "platformVersion", "null")]
    public void Nested_contracts_are_strict(string parent, string property, string value)
    {
        var node = JsonNode.Parse(Capture())!;
        node[parent]![property] = JsonNode.Parse(value);
        Assert.Throws<JsonException>(() => AssessmentSnapshotJson.Deserialize(node.ToJsonString()));
    }

    [Fact]
    public void Oversized_envelope_is_rejected_before_parsing()
    {
        Assert.Throws<JsonException>(() => AssessmentSnapshotJson.Deserialize(
            new string(' ', AssessmentJson.MaximumJsonCharacters + 1)));
    }

    private static string Capture() => new AssessmentSnapshotService().Capture(Pack("demo-a"), Case("demo-a-supported"), "test-1");
    private static string Pack(string demo) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", demo + "-pack.json"));
    private static string Case(string example) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Cases", example + ".json"));
}
