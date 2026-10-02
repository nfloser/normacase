using System.Text.Json.Nodes;
using NormaCase.Domain.Decision;
using NormaCase.Replay;
using NormaCase.Serialization;
using Xunit;

namespace NormaCase.Replay.Tests;

public sealed class NegationReplayTests
{
    [Theory]
    [InlineData("YES", AssessmentOutcome.NotSupported)]
    [InlineData("NO", AssessmentOutcome.Supported)]
    [InlineData("UNKNOWN", AssessmentOutcome.Incomplete)]
    public void Negated_rules_and_independent_outputs_roundtrip_in_complete_snapshots(
        string truth, AssessmentOutcome expected)
    {
        var pack = JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "demo-e-pack.json")))!;
        pack["manifest"]!["releaseId"] = "synthetic-negation-replay-1";
        foreach (var field in pack["fields"]!.AsArray()) field!["required"] = false;
        var originalCondition = pack["rules"]![0]!["condition"]!.DeepClone();
        var not = new JsonObject { ["kind"] = "not", ["conditions"] = new JsonArray(originalCondition) };
        pack["rules"]![0]!["condition"] = not;
        pack["outputs"]![0]!["condition"] = not.DeepClone();
        var input = JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Cases", "demo-e-mixed.json")))!;
        // This synthetic entry rule tests global_ready; other output facts remain as supplied.
        input["facts"]!["global_ready"] = truth == "UNKNOWN"
            ? JsonNode.Parse("""{"kind":"UNKNOWN"}""")
            : JsonNode.Parse("{\"kind\":\"TRUTH\",\"truth\":\"" + truth + "\"}");
        var service = new AssessmentSnapshotService();
        var captured = service.Capture(pack.ToJsonString(), input.ToJsonString(), "negation-test-platform");
        var restored = AssessmentSnapshotJson.Deserialize(captured);
        var replayed = service.Replay(captured, "negation-test-platform");
        Assert.Equal(expected, replayed.Assessment.Outcome);
        Assert.Equal(captured, service.Capture(pack.ToJsonString(), input.ToJsonString(), "negation-test-platform"));
        Assert.Equal(AssessmentJson.Serialize(restored.Assessment.Assessment, "negation-test-platform"),
            AssessmentJson.Serialize(replayed.Assessment, "negation-test-platform"));
        Assert.Equal("not", replayed.Assessment.RuleTrace!.Condition.Kind);
        Assert.Equal("not", replayed.Assessment.DomainOutputs[0].Condition.Kind);
        Assert.Single(replayed.Assessment.RuleTrace.Condition.Children);
    }
}
