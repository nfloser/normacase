using System.Text;
using System.Text.Json;
using NormaCase.Domain.Decision;
using NormaCase.Knowledge.Serialization;
using NormaCase.Knowledge.Validation;
using NormaCase.Serialization;

namespace NormaCase.Cli;

public static class CliRunner
{
    public static int Run(string[] args, TextWriter output, TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);
        if (args.Length == 1 && args[0] == "--help")
        {
            output.WriteLine(Messages.Get("Help"));
            return 0;
        }

        try
        {
            var options = Parse(args);
            var pack = new KnowledgePackLoader().LoadFromJson(ReadBoundedFile(options["--pack"]));
            if (pack.Manifest.ValidationLevel != "SYNTHETIC")
                throw new ArgumentException("This entry point accepts synthetic knowledge only.");
            var input = CaseInputJson.Deserialize(ReadBoundedFile(options["--case"]));
            var assessment = new NormaCase.RuleEngine.Evaluation.RuleEvaluator()
                .Evaluate(pack, input.Facts, input.AssessmentDate, input.Evidence);
            if (options.ContainsKey("--json"))
            {
                output.WriteLine(AssessmentJson.Serialize(assessment, options["--platform-version"]));
            }
            else
            {
                output.WriteLine(Messages.Get("SyntheticNotice"));
                output.WriteLine(Messages.Get("Outcome") + ": " + OutcomeLabel(assessment.Outcome));
                output.WriteLine(Messages.Get("Date") + ": " + assessment.AssessmentDate.ToString("d", Messages.Culture));
                output.WriteLine(Messages.Get("Release") + ": " + assessment.KnowledgeRelease);
                output.WriteLine(Messages.Get("PlatformVersion") + ": " + options["--platform-version"]);
                if (assessment.DomainOutputs.Count > 0)
                {
                    output.WriteLine(Messages.Get("DomainOutputs") + ":");
                    foreach (var domainOutput in assessment.DomainOutputs)
                    {
                        output.WriteLine("- " + domainOutput.OutputId + ": " + DomainOutputLabel(domainOutput.Value));
                    }
                }
                if (assessment.RuleTrace is not null)
                {
                    output.WriteLine(Messages.Get("Rule") + ": " + assessment.RuleTrace.RuleId + "@" + assessment.RuleTrace.RuleVersion);
                    output.WriteLine(Messages.Get("Source") + ": " + assessment.RuleTrace.SourceId);
                }
                if (assessment.MissingRequiredFields.Count > 0)
                    output.WriteLine(Messages.Get("Missing") + ": " + string.Join(", ", assessment.MissingRequiredFields));
                if (assessment.DomainOutputs.Count > 0)
                {
                    output.WriteLine(Messages.Get("DomainOutputs") + ":");
                    foreach (var domainOutput in assessment.DomainOutputs)
                    {
                        var value = domainOutput.Value.IsUnknown
                            ? Messages.Get("Unknown")
                            : domainOutput.Value.Choice!;
                        output.WriteLine("  " + domainOutput.OutputId + ": " + value);
                    }
                }
            }
            return 0;
        }
        catch (Exception exception) when (exception is JsonException or KnowledgeValidationException or ArgumentException or OverflowException or DecoderFallbackException)
        {
            error.WriteLine(Messages.Get("InputError"));
            return 2;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            error.WriteLine(Messages.Get("IoError"));
            return 3;
        }
    }

    private static string DomainOutputLabel(DomainOutputValue value) => value.Kind switch
    {
        DomainOutputValueKind.Unknown => Messages.Get("UnknownOutput"),
        DomainOutputValueKind.Choice when !string.IsNullOrWhiteSpace(value.Choice) => value.Choice,
        _ => throw new ArgumentException("Invalid domain output value.")
    };

    private static string OutcomeLabel(AssessmentOutcome outcome) => outcome switch
    {
        AssessmentOutcome.Supported => Messages.Get("Supported"),
        AssessmentOutcome.NotSupported => Messages.Get("NotSupported"),
        AssessmentOutcome.Incomplete => Messages.Get("Incomplete"),
        AssessmentOutcome.HumanReview => Messages.Get("HumanReview"),
        AssessmentOutcome.NotApplicable => Messages.Get("NotApplicable"),
        _ => throw new ArgumentException("Invalid outcome.")
    };

    private static Dictionary<string, string> Parse(string[] args)
    {
        if (args.Length == 0 || args[0] != "evaluate")
            throw new ArgumentException("Command required.");
        var options = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var index = 1; index < args.Length; index++)
        {
            var key = args[index];
            if (key == "--json")
            {
                if (!options.TryAdd(key, "true"))
                    throw new ArgumentException("Duplicate option.");
                continue;
            }
            if (key is not ("--pack" or "--case" or "--platform-version")
                || ++index >= args.Length || string.IsNullOrWhiteSpace(args[index])
                || args[index].StartsWith("--", StringComparison.Ordinal)
                || !options.TryAdd(key, args[index]))
                throw new ArgumentException("Invalid options.");
        }
        if (!options.ContainsKey("--pack") || !options.ContainsKey("--case") || !options.ContainsKey("--platform-version"))
            throw new ArgumentException("Required option missing.");
        return options;
    }

    private static string ReadBoundedFile(string path)
    {
        using var reader = new StreamReader(path, new UTF8Encoding(false, true));
        var buffer = new char[4096];
        var text = new StringBuilder();
        int count;
        while ((count = reader.Read(buffer, 0, buffer.Length)) > 0)
        {
            if (text.Length + count > AssessmentJson.MaximumJsonCharacters)
                throw new JsonException("Input exceeds supported size.");
            text.Append(buffer, 0, count);
        }
        return text.ToString();
    }
}
