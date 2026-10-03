using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
using NormaCase.Application.Intake;
using NormaCase.Domain.Cases;
using NormaCase.Domain.Decision;
using NormaCase.Domain.Evidence;
using NormaCase.Serialization;

namespace NormaCase.SyntheticIntegration;

// Explicit synthetic protocols only; not institutional or vendor interfaces.
public static class SyntheticIntakeAdapters
{
    public const int MaximumBytes = 64 * 1024;
    public static NormalizedIntakeRequest Json(string text, DateTimeOffset receivedAtUtc)
    {
        Bound(text);
        using var doc = JsonDocument.Parse(text, new JsonDocumentOptions { MaxDepth = 16 });
        var root = doc.RootElement;
        Properties(root, ["formatVersion", "order", "message", "revision", "input"]);
        if (root.GetProperty("formatVersion").GetInt32() != 1) throw new FormatException();
        var input = CaseInputJson.Deserialize(root.GetProperty("input").GetRawText());
        return Map("synthetic-json", root.GetProperty("order").GetString()!, root.GetProperty("message").GetString()!,
            Revision(root.GetProperty("revision").GetString()!), input.AssessmentDate, input.Facts, input.Evidence ?? new Dictionary<string, EvidenceStatus>(), receivedAtUtc);
    }
    public static NormalizedIntakeRequest Xml(string text, DateTimeOffset receivedAtUtc)
    {
        Bound(text);
        using var reader = XmlReader.Create(new StringReader(text), new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = MaximumBytes });
        var root = XDocument.Load(reader).Root ?? throw new FormatException();
        if (root.Name != "SyntheticCase" || root.Attributes().Any(a => !new[] { "formatVersion", "order", "message", "revision", "date" }.Contains(a.Name.ToString()))
            || root.Attribute("formatVersion")?.Value != "1") throw new FormatException();
        var facts = new Dictionary<string, CaseValue>(StringComparer.Ordinal);
        var evidence = new Dictionary<string, EvidenceStatus>(StringComparer.Ordinal);
        foreach (var element in root.Elements())
        {
            if (element.HasElements || element.Value.Length > 0 || element.Attributes().Any(a => !new[] { "id", "kind", "value" }.Contains(a.Name.ToString()))) throw new FormatException();
            var id = element.Attribute("id")?.Value ?? throw new FormatException();
            var value = element.Attribute("value")?.Value ?? throw new FormatException();
            if (element.Name == "Fact") facts.Add(id, element.Attribute("kind")?.Value switch
            {
                "TRUTH" => value switch { "YES" => TruthValue.Yes, "NO" => TruthValue.No, "UNKNOWN" => TruthValue.Unknown, _ => throw new FormatException() },
                "NUMBER" => ExactNumber(value),
                _ => throw new FormatException()
            });
            else if (element.Name == "Evidence" && element.Attribute("kind") is null) evidence.Add(id, value switch
            {
                "PRESENT" => EvidenceStatus.Present,
                "MISSING" => EvidenceStatus.Missing,
                _ => throw new FormatException()
            });
            else throw new FormatException();
        }
        return Map("synthetic-xml", (root.Attribute("order")?.Value ?? throw new FormatException()), (root.Attribute("message")?.Value ?? throw new FormatException()),
            Revision((root.Attribute("revision")?.Value ?? throw new FormatException())), DateOnly.ParseExact((root.Attribute("date")?.Value ?? throw new FormatException()), "yyyy-MM-dd", CultureInfo.InvariantCulture), facts, evidence, receivedAtUtc);
    }
    private static NormalizedIntakeRequest Map(string source, string order, string message, long revision, DateOnly date,
        IReadOnlyDictionary<string, CaseValue> facts, IReadOnlyDictionary<string, EvidenceStatus> evidence, DateTimeOffset received)
    {
        var id = "synthetic-intake-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source + ":" + order))).ToLowerInvariant();
        var refs = evidence.ToDictionary(item => item.Key, item => (IReadOnlyList<string>)(item.Value == EvidenceStatus.Present ? new[] { "synthetic-attachment-" + item.Key } : []), StringComparer.Ordinal);
        return new(new(id), "synthetic-reviewed", new(source, order, message, revision, source + "-adapter", 1, received), date, facts, evidence, refs);
    }
    private static CaseValue ExactNumber(string text)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(text, "\\A-?(0|[1-9][0-9]*)(\\.[0-9]+)?\\z", System.Text.RegularExpressions.RegexOptions.CultureInvariant)) throw new FormatException();
        var options = new JsonSerializerOptions();
        options.Converters.Add(new CaseValueJsonConverter());
        return JsonSerializer.Deserialize<CaseValue>("{\"kind\":\"NUMBER\",\"number\":" + text + "}", options);
    }
    private static long Revision(string text)
    {
        if (!long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var value) || value < 1 || value.ToString(CultureInfo.InvariantCulture) != text) throw new FormatException();
        return value;
    }
    private static void Bound(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        if (Encoding.UTF8.GetByteCount(text) > MaximumBytes) throw new FormatException();
    }
    private static void Properties(JsonElement element, string[] names)
    {
        if (element.ValueKind != JsonValueKind.Object) throw new FormatException();
        var actual = element.EnumerateObject().Select(p => p.Name).ToArray();
        if (actual.Length != names.Length || actual.Distinct(StringComparer.Ordinal).Count() != names.Length || actual.Except(names, StringComparer.Ordinal).Any()) throw new FormatException();
    }
}
