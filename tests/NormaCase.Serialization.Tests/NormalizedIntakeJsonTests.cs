using System.Text.Json;
using NormaCase.Application.Intake;
using NormaCase.Domain.Cases;
using NormaCase.Domain.Decision;
using NormaCase.Knowledge.Serialization;
using NormaCase.Serialization;
using Xunit;
namespace NormaCase.Serialization.Tests;

public sealed class NormalizedIntakeJsonTests
{
    [Fact]
    public void Historical_intake_replays_exact_values_and_original_provenance_without_current_knowledge()
    {
        var record = Record(); var json = NormalizedIntakeJson.Serialize(record); var restored = NormalizedIntakeJson.Deserialize(json);
        Assert.Equal(json, NormalizedIntakeJson.Serialize(restored)); Assert.True(record.HasSameContent(restored));
        Assert.Equal(15.1234567890123456789m, restored.Input.Facts["measurement"].Number);
        Assert.Equal("original-message", restored.Provenance.MessageId);
    }
    [Theory]
    [InlineData("\"formatVersion\":1", "\"formatVersion\":2")]
    [InlineData("\"formatVersion\":1", "\"formatVersion\":1,\"extra\":true")]
    [InlineData("\"formatVersion\":1", "\"formatVersion\":1,\"formatVersion\":1")]
    public void Malformed_or_unsupported_history_is_rejected(string from, string to) => Assert.Throws<JsonException>(() => NormalizedIntakeJson.Deserialize(NormalizedIntakeJson.Serialize(Record()).Replace(from, to)));
    private static NormalizedIntakeRecord Record()
    {
        var pack = new KnowledgePackLoader().LoadFromFile(Path.Combine(AppContext.BaseDirectory, "Fixtures/demo-c-pack.json"));
        return new NormalizedIntakeService(new Unused()).Normalize(new(new("synthetic-case"), "synthetic-type", new("synthetic-source", "upstream", "original-message", 1, "synthetic-adapter", 1, new(2026, 10, 3, 12, 0, 0, TimeSpan.Zero)), new(2026, 10, 3), new Dictionary<string, CaseValue> { { "measurement", 15.1234567890123456789m } }, new Dictionary<string, NormaCase.Domain.Evidence.EvidenceStatus>(), new Dictionary<string, IReadOnlyList<string>>()), pack);
    }
    private sealed class Unused : INormalizedIntakeStore { public Task<IntakeReceipt> AppendAsync(NormalizedIntakeRecord record, CancellationToken token = default) => throw new NotSupportedException(); }
}
