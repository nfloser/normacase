using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using NormaCase.Application.Intake;
using NormaCase.Domain.Cases;

namespace NormaCase.SyntheticIntegration.Intake;

/// <summary>
/// Synthetic XML intake format used only to prove adapter interchangeability.
/// DTDs and external entity resolution are prohibited.
/// </summary>
public sealed class SyntheticXmlIntakeAdapter
{
    public const string SourceSystemId = "synthetic-beta";
    public const string AdapterId = "synthetic-beta-adapter";
    public const int AdapterVersion = 1;

    public NormalizedIntakeRequest Parse(
        string xml,
        CaseId caseId,
        string caseTypeId,
        DateTimeOffset receivedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(xml))
            throw new ArgumentException("Synthetic XML intake is required.", nameof(xml));

        using var reader = XmlReader.Create(
            new StringReader(xml),
            new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                MaxCharactersInDocument = 8192,
                MaxCharactersFromEntities = 0,
                IgnoreComments = true,
                IgnoreProcessingInstructions = true
            });

        var root = XDocument.Load(reader, LoadOptions.None).Root
            ?? throw new ArgumentException("Synthetic XML intake requires a root element.", nameof(xml));
        if (root.Name != "SyntheticOrder")
            throw new ArgumentException("Synthetic XML intake has an invalid root.", nameof(xml));

        EnsureAttributes(root, "message", "order", "revision", "date");
        if (root.Elements().Any(element => element.Name != "Answer" && element.Name != "Attachment"))
            throw new ArgumentException("Synthetic XML intake contains an unknown element.", nameof(xml));

        var answers = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var answer in root.Elements("Answer"))
        {
            EnsureAttributes(answer, "name", "value");
            if (answer.HasElements || !string.IsNullOrWhiteSpace(answer.Value))
                throw new ArgumentException("Synthetic XML answer has invalid content.", nameof(xml));
            var name = RequiredAttribute(answer, "name");
            if (!answers.TryAdd(name, RequiredAttribute(answer, "value")))
                throw new ArgumentException("Duplicate synthetic answer.", nameof(xml));
        }

        var documents = new List<string>();
        foreach (var attachment in root.Elements("Attachment"))
        {
            EnsureAttributes(attachment, "reference");
            if (attachment.HasElements || !string.IsNullOrWhiteSpace(attachment.Value))
                throw new ArgumentException("Synthetic XML attachment has invalid content.", nameof(xml));
            documents.Add(RequiredAttribute(attachment, "reference"));
        }

        if (!long.TryParse(
                RequiredAttribute(root, "revision"),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var revision))
        {
            throw new ArgumentException("Synthetic XML revision is invalid.", nameof(xml));
        }

        return SyntheticIntakeMapping.Map(
            SourceSystemId,
            AdapterId,
            AdapterVersion,
            caseId,
            caseTypeId,
            RequiredAttribute(root, "order"),
            RequiredAttribute(root, "message"),
            revision,
            RequiredAttribute(root, "date"),
            answers,
            documents,
            receivedAtUtc);
    }

    private static string RequiredAttribute(XElement element, string name)
    {
        var value = element.Attribute(name)?.Value;
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Synthetic XML intake requires a non-empty attribute.");
        return value;
    }

    private static void EnsureAttributes(XElement element, params string[] names)
    {
        var allowed = names.ToHashSet(StringComparer.Ordinal);
        if (element.Attributes().Any(attribute => !allowed.Contains(attribute.Name.LocalName)))
            throw new ArgumentException("Synthetic XML intake contains an unknown attribute.");
        foreach (var name in names)
            if (element.Attribute(name) is null)
                throw new ArgumentException("Synthetic XML intake is missing a required attribute.");
    }
}
