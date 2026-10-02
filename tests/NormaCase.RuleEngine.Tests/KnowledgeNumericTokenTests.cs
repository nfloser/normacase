using System.Text.Json;
using System.Text.Json.Nodes;
using NormaCase.Knowledge.Serialization;
using Xunit;

namespace NormaCase.RuleEngine.Tests;

public sealed class KnowledgeNumericTokenTests
{
    [Theory]
    [InlineData("demo-a", "formatVersion")]
    [InlineData("demo-a", "version")]
    [InlineData("demo-b", "threshold")]
    [InlineData("demo-b", "minimum")]
    [InlineData("demo-b", "maximum")]
    [InlineData("demo-d", "value")]
    public void Quoted_numeric_tokens_are_rejected_without_coercion(string demo, string property)
    {
        var node = JsonNode.Parse(Read(demo))!;
        Assert.True(QuoteFirstNumber(node, property), "Synthetic fixture must exercise the requested numeric property.");
        Assert.Throws<JsonException>(() => new KnowledgePackLoader().LoadFromJson(node.ToJsonString()));
    }

    [Theory]
    [InlineData("demo-a")]
    [InlineData("demo-b")]
    [InlineData("demo-c")]
    [InlineData("demo-d")]
    [InlineData("demo-e")]
    public void Existing_numeric_tokens_remain_supported(string demo)
    {
        Assert.Equal(demo + "-2026.1", new KnowledgePackLoader().LoadFromJson(Read(demo)).Manifest.ReleaseId);
    }

    [Fact]
    public void Exact_decimal_threshold_is_preserved()
    {
        var node = JsonNode.Parse(Read("demo-b"))!;
        Assert.True(SetFirstThreshold(node));
        var json = node.ToJsonString();
        var pack = new KnowledgePackLoader().LoadFromJson(json);
        Assert.Contains("123456789.1234567890123456789", json, StringComparison.Ordinal);
        Assert.Contains(pack.Rules.SelectMany(r => Thresholds(r.Condition)),
            threshold => threshold == 123456789.1234567890123456789m);
    }

    private static IEnumerable<decimal> Thresholds(NormaCase.Knowledge.Model.ConditionDefinition condition)
    {
        if (condition.Threshold is { } value) yield return value;
        foreach (var child in condition.Conditions)
            foreach (var threshold in Thresholds(child)) yield return threshold;
    }

    private static bool SetFirstThreshold(JsonNode node)
    {
        if (node is JsonObject obj)
        {
            if (obj.ContainsKey("threshold"))
            {
                obj["threshold"] = 123456789.1234567890123456789m;
                return true;
            }
            foreach (var pair in obj)
                if (pair.Value is { } child && SetFirstThreshold(child)) return true;
        }
        if (node is JsonArray array)
            foreach (var child in array)
                if (child is not null && SetFirstThreshold(child)) return true;
        return false;
    }

    private static bool QuoteFirstNumber(JsonNode node, string property)
    {
        if (node is JsonObject obj)
        {
            foreach (var pair in obj)
            {
                if (pair.Key == property && pair.Value is JsonValue value &&
                    value.TryGetValue<JsonElement>(out var element) && element.ValueKind == JsonValueKind.Number)
                {
                    obj[property] = element.GetRawText();
                    return true;
                }
                if (pair.Value is { } child && QuoteFirstNumber(child, property)) return true;
            }
        }
        if (node is JsonArray array)
            foreach (var child in array)
                if (child is not null && QuoteFirstNumber(child, property)) return true;
        return false;
    }

    private static string Read(string demo) => File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "Fixtures", demo + "-pack.json"));
}
