using System.Globalization;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using NormaCase.Domain.Cases;
using NormaCase.Domain.Decision;
using NormaCase.Domain.Evidence;
using NormaCase.Knowledge.Model;
using NormaCase.Knowledge.Serialization;
using NormaCase.Knowledge.Validation;
using NormaCase.RuleEngine.Evaluation;
using NormaCase.Serialization;

namespace NormaCase.Api;

public static class DemoHost
{
    public const int MaximumBodyBytes = 1024 * 1024;

    public static WebApplication Build(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.WebHost.ConfigureKestrel(options =>
        {
            options.ListenLocalhost(5080);
            options.Limits.MaxRequestBodySize = MaximumBodyBytes;
        });
        builder.Logging.ClearProviders();
        var packs = Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "Knowledge"), "*.json")
            .Select(path => new KnowledgePackLoader().LoadFromFile(path))
            .ToDictionary(pack => pack.Manifest.PackId, StringComparer.Ordinal);
        if (packs.Count == 0 || packs.Values.Any(pack => pack.Manifest.ValidationLevel != "SYNTHETIC"))
            throw new InvalidOperationException("Local demo catalog must contain synthetic knowledge only.");
        var presentations = PresentationCatalog.Load(packs);
        var platformVersion = typeof(DemoHost).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion;
        var app = builder.Build();

        app.UseExceptionHandler(handler => handler.Run(async context =>
        {
            context.Response.StatusCode = 500;
            context.Response.Headers.CacheControl = "no-store";
            context.Response.Headers["X-Content-Type-Options"] = "nosniff";
            await context.Response.WriteAsJsonAsync(new { code = "internal_error", message = ApiMessages.Get("internal_error") });
        }));
        app.Use(async (context, next) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            context.Response.Headers["X-Content-Type-Options"] = "nosniff";
            var host = context.Request.Host.Host;
            var localHost = host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
                || host == "127.0.0.1" || host == "::1" || host == "[::1]";
            var remote = context.Connection.RemoteIpAddress;
            if (!localHost || (remote is not null && !IPAddress.IsLoopback(remote))
                || !AllowedOrigin(context.Request))
            {
                await Error("local_only", 403).ExecuteAsync(context);
                return;
            }
            await next(context);
        });

        app.MapGet("/api/packs", () => packs.Values.OrderBy(pack => pack.Manifest.PackId, StringComparer.Ordinal)
            .Select(pack =>
            {
                var presentation = presentations[pack.Manifest.PackId];
                return new
                {
                    packId = pack.Manifest.PackId,
                    releaseId = pack.Manifest.ReleaseId,
                    validationLevel = pack.Manifest.ValidationLevel,
                    presentation = new
                    {
                        locale = presentation.Locale,
                        name = presentation.Name,
                        description = presentation.Description,
                        outputs = presentation.Outputs
                            .OrderBy(item => item.Key, StringComparer.Ordinal)
                            .Select(item => new
                            {
                                id = item.Key,
                                label = item.Value.Label,
                                choices = item.Value.Choices
                                    .OrderBy(choice => choice.Key, StringComparer.Ordinal)
                                    .ToDictionary(choice => choice.Key, choice => choice.Value, StringComparer.Ordinal)
                            })
                            .ToArray(),
                        examples = presentation.Examples
                            .Select(item => new { id = item.Id, label = item.Label })
                            .ToArray()
                    },
                    fields = pack.Fields.Select(field =>
                    {
                        var text = presentation.Fields[field.Id];
                        return new
                        {
                            id = field.Id,
                            type = field.Type,
                            required = field.Required,
                            label = text.Label,
                            helpText = text.HelpText
                        };
                    }).ToArray(),
                    evidenceRequirements = pack.EvidenceRequirements.Select(item =>
                    {
                        var text = presentation.EvidenceRequirements[item.Id];
                        return new { id = item.Id, label = text.Label, helpText = text.HelpText };
                    }).ToArray()
                };
            }).ToArray());

        app.MapGet("/api/packs/{packId}/examples/{exampleId}", (string packId, string exampleId) =>
        {
            if (!presentations.TryGetValue(packId, out var presentation))
                return Error("unknown_pack", 404);

            var example = presentation.Examples.SingleOrDefault(
                item => string.Equals(item.Id, exampleId, StringComparison.Ordinal));
            if (example is null)
                return Error("unknown_example", 404);

            return Results.Json(new
            {
                assessmentDate = example.Input.AssessmentDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                values = pack.Fields.ToDictionary(
                    field => field.Id,
                    field => ExampleValue(example.Input.Facts.TryGetValue(field.Id, out var value)
                        ? value
                        : CaseValue.Unknown),
                    StringComparer.Ordinal),
                evidence = pack.EvidenceRequirements.ToDictionary(
                    item => item.Id,
                    item => example.Input.Evidence is not null
                        && example.Input.Evidence.TryGetValue(item.Id, out var status)
                            ? status.ToString().ToUpperInvariant()
                            : "MISSING",
                    StringComparer.Ordinal)
            });
        });

        app.MapPost("/api/assessments/{packId}", async (string packId, HttpRequest request) =>
        {
            if (!packs.TryGetValue(packId, out var pack))
                return Error("unknown_pack", 404);
            if (!request.HasJsonContentType())
                return Error("json_required", 415);
            if (request.ContentLength > MaximumBodyBytes)
                return Error("input_too_large", 413);
            try
            {
                using var reader = new StreamReader(request.Body, new UTF8Encoding(false, true));
                var buffer = new char[4096];
                var text = new StringBuilder();
                int read;
                while ((read = await reader.ReadAsync(buffer, request.HttpContext.RequestAborted)) > 0)
                {
                    if (text.Length + read > MaximumBodyBytes)
                        return Error("input_too_large", 413);
                    text.Append(buffer, 0, read);
                }
                var input = CaseInputJson.Deserialize(text.ToString());
                var result = new RuleEvaluator().Evaluate(pack, input.Facts, input.AssessmentDate, input.Evidence);
                return Results.Content(AssessmentJson.Serialize(result, platformVersion), "application/json", Encoding.UTF8);
            }
            catch (BadHttpRequestException exception) when (exception.StatusCode == 413)
            {
                return Error("input_too_large", 413);
            }
            catch (Exception exception) when (exception is JsonException or KnowledgeValidationException or ArgumentException or OverflowException)
            {
                return Error("invalid_input", 400);
            }
        });

        return app;
    }

    private static string ExampleValue(CaseValue value)
        => value.Kind switch
        {
            CaseValueKind.Unknown => "UNKNOWN",
            CaseValueKind.Truth => value.Truth switch
            {
                TruthValue.Yes => "YES",
                TruthValue.No => "NO",
                TruthValue.NotApplicable => "NOT_APPLICABLE",
                _ => "UNKNOWN"
            },
            CaseValueKind.Number => value.Number!.Value.ToString(CultureInfo.InvariantCulture),
            _ => "UNKNOWN"
        };

    private static bool AllowedOrigin(HttpRequest request)
    {
        if (!request.Headers.TryGetValue("Origin", out var values))
            return true;
        if (values.Count != 1 || !Uri.TryCreate(values[0], UriKind.Absolute, out var origin))
            return false;
        var expectedPort = request.Host.Port ?? (request.Scheme == "https" ? 443 : 80);
        return origin.Scheme == request.Scheme
            && origin.Host.Equals(request.Host.Host.Trim('[', ']'), StringComparison.OrdinalIgnoreCase)
            && origin.Port == expectedPort
            && origin.AbsolutePath == "/" && string.IsNullOrEmpty(origin.Query)
            && string.IsNullOrEmpty(origin.Fragment) && string.IsNullOrEmpty(origin.UserInfo);
    }

    private static IResult Error(string code, int status)
        => Results.Json(new { code, message = ApiMessages.Get(code) }, statusCode: status);
}
