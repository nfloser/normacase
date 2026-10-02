using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using NormaCase.Knowledge.Validation;
using NormaCase.RuleEngine.Evaluation;
using NormaCase.Serialization;

namespace NormaCase.Api;

public static class ApiHost
{
    public const int MaximumRequestBytes = 8 * 1024 * 1024;

    private static readonly UTF8Encoding StrictUtf8 = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    public static WebApplication Build(ApiHostOptions options)
    {
        ValidateOptions(options);

        var catalog = SyntheticPackCatalog.Load(options.KnowledgeRoot);
        var builder = WebApplication.CreateSlimBuilder(
            new WebApplicationOptions { Args = [] });

        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(server =>
        {
            server.AddServerHeader = false;
            server.Limits.MaxRequestBodySize =
                MaximumRequestBytes + (64 * 1024);
            server.Listen(
                IPAddress.Loopback,
                options.Port,
                listen => listen.Protocols = HttpProtocols.Http1AndHttp2);
        });

        builder.Services.AddSingleton(catalog);
        builder.Services.AddSingleton<RuleEvaluator>();

        var app = builder.Build();

        app.Use(async (context, next) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            context.Response.Headers.Pragma = "no-cache";
            context.Response.Headers["X-Content-Type-Options"] = "nosniff";
            await next();
        });

        app.Use(async (context, next) =>
        {
            if (!IsLoopbackHost(context.Request.Host.Host)
                || !IsLocalOrigin(context.Request.Headers["Origin"]))
            {
                await Error(
                        StatusCodes.Status403Forbidden,
                        "local_only",
                        Messages.Get("LocalOnly"))
                    .ExecuteAsync(context);
                return;
            }

            await next();
        });

        app.MapGet(
            "/api/v1/packs",
            (SyntheticPackCatalog packCatalog)
                => Results.Json(
                    new PackCatalogResponse(packCatalog.Items)));

        app.MapPost(
            "/api/v1/packs/{packId}/assessments",
            async (
                HttpContext context,
                string packId,
                SyntheticPackCatalog packCatalog,
                RuleEvaluator evaluator) =>
            {
                if (!packCatalog.TryGet(packId, out var pack))
                {
                    return Error(
                        StatusCodes.Status404NotFound,
                        "pack_not_found",
                        Messages.Get("UnknownPack"));
                }

                if (!IsApplicationJson(context.Request.ContentType))
                {
                    return Error(
                        StatusCodes.Status415UnsupportedMediaType,
                        "unsupported_media_type",
                        Messages.Get("UnsupportedMediaType"));
                }

                if (context.Request.ContentLength is > MaximumRequestBytes)
                {
                    return Error(
                        StatusCodes.Status413PayloadTooLarge,
                        "request_too_large",
                        Messages.Get("RequestTooLarge"));
                }

                try
                {
                    var json = await ReadBoundedBodyAsync(
                        context.Request,
                        context.RequestAborted);
                    var input = CaseInputJson.Deserialize(json);
                    var assessment = evaluator.Evaluate(
                        pack,
                        input.Facts,
                        input.AssessmentDate,
                        input.Evidence);

                    return Results.Text(
                        AssessmentJson.Serialize(
                            assessment,
                            options.PlatformVersion),
                        "application/json",
                        Encoding.UTF8,
                        StatusCodes.Status200OK);
                }
                catch (RequestTooLargeException)
                {
                    return Error(
                        StatusCodes.Status413PayloadTooLarge,
                        "request_too_large",
                        Messages.Get("RequestTooLarge"));
                }
                catch (BadHttpRequestException exception)
                    when (exception.StatusCode
                        == StatusCodes.Status413PayloadTooLarge)
                {
                    return Error(
                        StatusCodes.Status413PayloadTooLarge,
                        "request_too_large",
                        Messages.Get("RequestTooLarge"));
                }
                catch (Exception exception)
                    when (exception is JsonException
                        or KnowledgeValidationException
                        or ArgumentException
                        or OverflowException
                        or DecoderFallbackException)
                {
                    return Error(
                        StatusCodes.Status400BadRequest,
                        "invalid_input",
                        Messages.Get("InvalidInput"));
                }
            });

        return app;
    }

    public static async Task<int> RunAsync(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        if (args.Length == 1
            && string.Equals(args[0], "--help", StringComparison.Ordinal))
        {
            Console.Out.WriteLine(Messages.Get("Help"));
            return 0;
        }

        try
        {
            var options = Parse(args);
            await using var app = Build(options);
            await app.RunAsync();
            return 0;
        }
        catch (Exception exception)
            when (exception is ArgumentException
                or FormatException
                or OverflowException)
        {
            Console.Error.WriteLine(Messages.Get("StartupError"));
            return 2;
        }
        catch (Exception exception)
            when (exception is IOException
                or UnauthorizedAccessException
                or JsonException
                or KnowledgeValidationException)
        {
            Console.Error.WriteLine(Messages.Get("KnowledgeError"));
            return 3;
        }
    }

    private static ApiHostOptions Parse(string[] args)
    {
        var values = new Dictionary<string, string>(
            StringComparer.Ordinal);

        for (var index = 0; index < args.Length; index++)
        {
            var key = args[index];
            if (key is not (
                "--knowledge-root"
                or "--platform-version"
                or "--port"))
            {
                throw new ArgumentException("Unknown option.");
            }

            if (++index >= args.Length
                || string.IsNullOrWhiteSpace(args[index])
                || args[index].StartsWith(
                    "--",
                    StringComparison.Ordinal)
                || !values.TryAdd(key, args[index]))
            {
                throw new ArgumentException("Invalid option.");
            }
        }

        if (!values.TryGetValue(
                "--knowledge-root",
                out var knowledgeRoot)
            || !values.TryGetValue(
                "--platform-version",
                out var platformVersion))
        {
            throw new ArgumentException("Required option missing.");
        }

        var port = 5099;
        if (values.TryGetValue("--port", out var portText)
            && (!int.TryParse(portText, out port)
                || port is < 1 or > 65535))
        {
            throw new ArgumentException("Invalid port.");
        }

        return new(
            knowledgeRoot,
            platformVersion,
            port);
    }

    private static void ValidateOptions(ApiHostOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(
            options.KnowledgeRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(
            options.PlatformVersion);

        if (options.Port is < 0 or > 65535)
            throw new ArgumentOutOfRangeException(nameof(options));
    }

    private static bool IsLoopbackHost(string host)
    {
        if (string.Equals(
            host,
            "localhost",
            StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return IPAddress.TryParse(host, out var address)
            && IPAddress.IsLoopback(address);
    }

    private static bool IsLocalOrigin(
        Microsoft.Extensions.Primitives.StringValues origins)
    {
        if (origins.Count == 0)
            return true;

        if (origins.Count != 1
            || !Uri.TryCreate(
                origins[0],
                UriKind.Absolute,
                out var origin)
            || origin.Scheme is not ("http" or "https"))
        {
            return false;
        }

        return IsLoopbackHost(origin.Host);
    }

    private static bool IsApplicationJson(string? contentType)
    {
        if (!MediaTypeHeaderValue.TryParse(
                contentType,
                out var parsed)
            || !string.Equals(
                parsed.MediaType,
                "application/json",
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return string.IsNullOrWhiteSpace(parsed.CharSet)
            || string.Equals(
                parsed.CharSet.Trim('"'),
                "utf-8",
                StringComparison.OrdinalIgnoreCase)
            || string.Equals(
                parsed.CharSet.Trim('"'),
                "utf8",
                StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<string> ReadBoundedBodyAsync(
        HttpRequest request,
        CancellationToken cancellationToken)
    {
        using var stream = new MemoryStream();
        var buffer = new byte[16 * 1024];
        var total = 0;

        while (true)
        {
            var count = await request.Body.ReadAsync(
                buffer,
                cancellationToken);
            if (count == 0)
                break;

            total = checked(total + count);
            if (total > MaximumRequestBytes)
                throw new RequestTooLargeException();

            await stream.WriteAsync(
                buffer.AsMemory(0, count),
                cancellationToken);
        }

        return StrictUtf8.GetString(stream.ToArray());
    }

    private static IResult Error(
        int statusCode,
        string code,
        string message)
        => Results.Json(
            new ApiError(code, message),
            statusCode: statusCode);

    private sealed class RequestTooLargeException : Exception;
}
