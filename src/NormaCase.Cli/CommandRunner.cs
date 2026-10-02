using System.Globalization;
using System.Text.Json;
using NormaCase.Domain.Decision;
using NormaCase.Knowledge.Serialization;
using NormaCase.Knowledge.Validation;
using NormaCase.RuleEngine.Evaluation;
using NormaCase.Serialization;

namespace NormaCase.Cli;

public static class CommandRunner
{
    public static async Task<int> RunAsync(
        string[] args,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        if (args.Length == 0 || args is ["--help"] or ["-h"])
        {
            await output.WriteLineAsync(CliMessages.Get("Help"));
            return ExitCodes.Success;
        }

        if (!string.Equals(args[0], "evaluate", StringComparison.Ordinal))
        {
            await error.WriteLineAsync(CliMessages.Get("UsageError"));
            return ExitCodes.InputOrKnowledgeFailure;
        }

        if (!TryParseArguments(args[1..], out var options))
        {
            await error.WriteLineAsync(CliMessages.Get("UsageError"));
            return ExitCodes.InputOrKnowledgeFailure;
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var pack = new KnowledgePackLoader().LoadFromFile(options.PackPath);
            var caseJson = await File.ReadAllTextAsync(options.CasePath, cancellationToken);
            var input = CaseInputJson.Deserialize(caseJson);

            var result = new RuleEvaluator().Evaluate(
                pack,
                input.Facts,
                options.AssessmentDate,
                input.Evidence);

            await WriteHumanResultAsync(output, result);

            if (options.OutputPath is not null)
            {
                var assessmentJson = AssessmentJson.Serialize(result, options.PlatformVersion!);
                await File.WriteAllTextAsync(options.OutputPath, assessmentJson, cancellationToken);
                await output.WriteLineAsync(string.Format(
                    CultureInfo.GetCultureInfo("de-DE"),
                    CliMessages.Get("OutputWritten"),
                    options.OutputPath));
            }

            return ExitCodes.Success;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (IOException)
        {
            await error.WriteLineAsync(CliMessages.Get("IoError"));
            return ExitCodes.IoFailure;
        }
        catch (UnauthorizedAccessException)
        {
            await error.WriteLineAsync(CliMessages.Get("IoError"));
            return ExitCodes.IoFailure;
        }
        catch (KnowledgeValidationException)
        {
            await error.WriteLineAsync(CliMessages.Get("KnowledgeError"));
            return ExitCodes.InputOrKnowledgeFailure;
        }
        catch (JsonException)
        {
            await error.WriteLineAsync(CliMessages.Get("InputError"));
            return ExitCodes.InputOrKnowledgeFailure;
        }
        catch (ArgumentException)
        {
            await error.WriteLineAsync(CliMessages.Get("InputError"));
            return ExitCodes.InputOrKnowledgeFailure;
        }
        catch (InvalidOperationException)
        {
            await error.WriteLineAsync(CliMessages.Get("KnowledgeError"));
            return ExitCodes.InputOrKnowledgeFailure;
        }
    }

    private static async Task WriteHumanResultAsync(TextWriter output, AssessmentResult result)
    {
        await output.WriteLineAsync(string.Format(
            CultureInfo.GetCultureInfo("de-DE"),
            CliMessages.Get("Outcome"),
            OutcomeLabel(result.Outcome)));

        await output.WriteLineAsync(string.Format(
            CultureInfo.GetCultureInfo("de-DE"),
            CliMessages.Get("KnowledgeRelease"),
            result.KnowledgeRelease));

        await output.WriteLineAsync(string.Format(
            CultureInfo.GetCultureInfo("de-DE"),
            CliMessages.Get("AssessmentDate"),
            result.AssessmentDate.ToString("dd.MM.yyyy", CultureInfo.GetCultureInfo("de-DE"))));

        if (result.MissingRequiredFields.Count > 0)
        {
            await output.WriteLineAsync(string.Format(
                CultureInfo.GetCultureInfo("de-DE"),
                CliMessages.Get("MissingFields"),
                string.Join(", ", result.MissingRequiredFields)));
        }
    }

    private static string OutcomeLabel(AssessmentOutcome outcome)
        => outcome switch
        {
            AssessmentOutcome.Supported => CliMessages.Get("OutcomeSupported"),
            AssessmentOutcome.NotSupported => CliMessages.Get("OutcomeNotSupported"),
            AssessmentOutcome.Incomplete => CliMessages.Get("OutcomeIncomplete"),
            AssessmentOutcome.HumanReview => CliMessages.Get("OutcomeHumanReview"),
            AssessmentOutcome.NotApplicable => CliMessages.Get("OutcomeNotApplicable"),
            _ => throw new InvalidOperationException("Unknown assessment outcome.")
        };

    private static bool TryParseArguments(string[] args, out Options options)
    {
        string? packPath = null;
        string? casePath = null;
        string? outputPath = null;
        string? platformVersion = null;
        DateOnly? assessmentDate = null;

        for (var i = 0; i < args.Length; i++)
        {
            if (i + 1 >= args.Length)
            {
                options = default!;
                return false;
            }

            var name = args[i];
            var value = args[++i];

            switch (name)
            {
                case "--pack" when packPath is null:
                    packPath = value;
                    break;
                case "--case" when casePath is null:
                    casePath = value;
                    break;
                case "--date" when assessmentDate is null
                    && DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed):
                    assessmentDate = parsed;
                    break;
                case "--output" when outputPath is null:
                    outputPath = value;
                    break;
                case "--platform-version" when platformVersion is null:
                    platformVersion = value;
                    break;
                default:
                    options = default!;
                    return false;
            }
        }

        if (string.IsNullOrWhiteSpace(packPath)
            || string.IsNullOrWhiteSpace(casePath)
            || assessmentDate is null
            || (outputPath is not null && string.IsNullOrWhiteSpace(platformVersion))
            || (outputPath is null && platformVersion is not null))
        {
            options = default!;
            return false;
        }

        options = new(packPath, casePath, assessmentDate.Value, outputPath, platformVersion);
        return true;
    }

    private sealed record Options(
        string PackPath,
        string CasePath,
        DateOnly AssessmentDate,
        string? OutputPath,
        string? PlatformVersion);
}
