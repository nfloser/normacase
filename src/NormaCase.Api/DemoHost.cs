using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
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
        var webRoot = Path.Combine(AppContext.BaseDirectory, "wwwroot");
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = args,
            WebRootPath = Directory.Exists(webRoot) ? webRoot : null
        });
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
            context.Response.Headers["Content-Security-Policy"] = "default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self'; connect-src 'self'; object-src 'none'; base-uri 'none'; frame-ancestors 'none'";
            await context.Response.WriteAsJsonAsync(new { code = "internal_error", message = ApiMessages.Get("internal_error") });
        }));
        app.Use(async (context, next) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            context.Response.Headers["X-Content-Type-Options"] = "nosniff";
            context.Response.Headers["Content-Security-Policy"] = "default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self'; connect-src 'self'; object-src 'none'; base-uri 'none'; frame-ancestors 'none'";
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

        app.UseDefaultFiles();
        app.UseStaticFiles();

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
                            .Select(item => new { id = item.Key, label = item.Value.Label })
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
            return example is null
                ? Error("unknown_example", 404)
                : Results.Content(example.Json, "application/json", Encoding.UTF8);
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
