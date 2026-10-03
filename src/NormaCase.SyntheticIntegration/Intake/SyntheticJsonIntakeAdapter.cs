using System.Globalization;
using System.Text.Json;
using NormaCase.Application.Intake;
using NormaCase.Domain.Cases;
using NormaCase.Domain.Decision;
using NormaCase.Domain.Evidence;

namespace NormaCase.SyntheticIntegration.Intake;

/// <summary>
/// Synthetic JSON intake format used only to prove adapter interchangeability.
/// It is not an MD or vendor contract.
/// </summary>
public sealed class SyntheticJsonIntakeAdapter
{
    public const string SourceSystemId = "synthetic-alpha";
    public const string AdapterId = "synthetic-alpha-adapter";
    public const int AdapterVersion = 1;

    public NormalizedIntakeRequest Parse(
        string json,
        CaseId caseId,
        string caseTypeId,
        DateTimeOffset receivedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new ArgumentException("Synthetic JSON intake is required.", nameof(json));

        using var document = JsonDocument.Parse(
            json,
            new JsonDocumentOptions
            {
                MaxDepth = 8,
                CommentHandling = JsonCommentHandling.Disallow,
                AllowTrailingCommas = false
            });

        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
            throw new ArgumentException("Synthetic JSON intake must be an object.", nameof(json));

        EnsureOnly(
            root,
            "message", "order", "revision", "date", "answers", "documents");

        var answersElement = Required(root, "answers", JsonValueKind.Object);
        var answers = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var property in answersElement.EnumerateObject())
        {
            if (!answers.TryAdd(property.Name, RequiredString(property.Value)))
                throw new ArgumentException("Duplicate synthetic answer.", nameof(json));
        }

        var documentsElement = Required(root, "documents", JsonValueKind.Array);
        var documents = documentsElement
            .EnumerateArray()
            .Select(RequiredString)
            .ToArray();

        return SyntheticIntakeMapping.Map(
            SourceSystemId,
            AdapterId,
            AdapterVersion,
            caseId,
            caseTypeId,
            RequiredString(root, "order"),
            RequiredString(root, "message"),
            RequiredLong(root, "revision"),
            RequiredString(root, "date"),
            answers,
            documents,
            receivedAtUtc);
    }

    private static JsonElement Required(
        JsonElement root,
        string name,
        JsonValueKind kind)
    {
        if (!root.TryGetProperty(name, out var value) || value.ValueKind != kind)
            throw new ArgumentException("Synthetic JSON intake has an invalid shape.");
        return value;
    }

    private static string RequiredString(JsonElement root, string name)
        => RequiredString(Required(root, name, JsonValueKind.String));

    private static string RequiredString(JsonElement value)
    {
        var text = value.GetString();
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Synthetic JSON intake requires a non-empty string.");
        return text;
    }

    private static long RequiredLong(JsonElement root, string name)
    {
        var value = Required(root, name, JsonValueKind.Number);
        if (!value.TryGetInt64(out var parsed))
            throw new ArgumentException("Synthetic JSON revision is invalid.");
        return parsed;
    }

    private static void EnsureOnly(JsonElement root, params string[] names)
    {
        var allowed = names.ToHashSet(StringComparer.Ordinal);
        if (root.EnumerateObject().Any(property => !allowed.Contains(property.Name)))
            throw new ArgumentException("Synthetic JSON intake contains an unknown property.");
        foreach (var name in names)
            if (!root.TryGetProperty(name, out _))
                throw new ArgumentException("Synthetic JSON intake is missing a required property.");
    }
}

internal static class SyntheticIntakeMapping
{
    internal static NormalizedIntakeRequest Map(
        string sourceSystemId,
        string adapterId,
        int adapterVersion,
        CaseId caseId,
        string caseTypeId,
        string upstreamCaseId,
        string messageId,
        long revision,
        string assessmentDate,
        IReadOnlyDictionary<string, string> answers,
        IReadOnlyList<string> documents,
        DateTimeOffset receivedAtUtc)
    {
        if (answers.Count > 256 || documents.Count > 32)
            throw new ArgumentException("Synthetic intake exceeds its bounded collection size.");

        var unknownAnswers = answers.Keys
            .Where(key => key is not "confirmed" and not "measurement")
            .ToArray();
        if (unknownAnswers.Length != 0)
            throw new ArgumentException("Synthetic intake contains an unmapped answer.");

        var facts = new Dictionary<string, CaseValue>(StringComparer.Ordinal);
        if (answers.TryGetValue("confirmed", out var truth))
        {
            facts["request_confirmed"] = truth.ToUpperInvariant() switch
            {
                "YES" => TruthValue.Yes,
                "NO" => TruthValue.No,
                "UNKNOWN" => TruthValue.Unknown,
                _ => throw new ArgumentException("Unmapped synthetic truth value.")
            };
        }

        if (answers.TryGetValue("measurement", out var number))
        {
            if (!decimal.TryParse(
                    number,
                    NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
                    CultureInfo.InvariantCulture,
                    out var parsed))
            {
                throw new ArgumentException("Unmapped synthetic numeric value.");
            }
            facts["measurement"] = parsed;
        }

        if (!DateOnly.TryParseExact(
                assessmentDate,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var date))
        {
            throw new ArgumentException("Synthetic intake date is invalid.");
        }

        return new NormalizedIntakeRequest(
            caseId,
            caseTypeId,
            new IntakeProvenance(
                sourceSystemId,
                upstreamCaseId,
                messageId,
                revision,
                adapterId,
                adapterVersion,
                receivedAtUtc),
            date,
            facts,
            new Dictionary<string, EvidenceStatus>(StringComparer.Ordinal)
            {
                ["verification"] = documents.Count > 0
                    ? EvidenceStatus.Present
                    : EvidenceStatus.Missing
            },
            new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
            {
                ["verification"] = documents.ToArray()
            });
    }
}
