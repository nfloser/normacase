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
using NormaCase.Replay;

namespace NormaCase.Api;

public static class DemoHost
{
    public const int MaximumBodyBytes = 1024 * 1024;

    public static WebApplication Build(string[] args)
    {
        var webRoot = Path.Combine(AppContext.BaseDirectory, "wwwroot");
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = args, WebRootPath = Directory.Exists(webRoot) ? webRoot : null });
        builder.WebHost.ConfigureKestrel(options =>
        {
            options.ListenLocalhost(5080);
            options.Limits.MaxRequestBodySize = MaximumBodyBytes;
        });
        builder.Logging.ClearProviders();
        builder.Services.AddSingleton(services => SyntheticReviewCredential.Load(
            services.GetRequiredService<IConfiguration>()));
        builder.Services.AddSingleton(services =>
        {
            var reviewConnection = services.GetRequiredService<IConfiguration>()
                .GetConnectionString("SyntheticReview");
            if (string.IsNullOrWhiteSpace(reviewConnection))
                throw new InvalidOperationException("Persistent synthetic review requires an explicit PostgreSQL connection.");
            return Npgsql.NpgsqlDataSource.Create(reviewConnection);
        });
        builder.Services.AddSingleton(services =>
        {
            var configuration = services.GetRequiredService<IConfiguration>();
            return new SyntheticIdentityAccessGate(
                configuration.GetValue<bool>("SyntheticReview:PersistenceEnabled")
                    ? new NormaCase.Persistence.PostgreSql.PostgresIdentityAccessAdministrationStore(
                        services.GetRequiredService<Npgsql.NpgsqlDataSource>())
                    : null);
        });
        builder.Services.AddAuthentication(SyntheticReviewAuthentication.SchemeName)
            .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions,
                SyntheticReviewAuthentication>(SyntheticReviewAuthentication.SchemeName, _ => { });
        builder.Services.AddAuthorization();
        var catalog = Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "Knowledge"), "*.json")
            .Select(path => File.ReadAllText(path, new UTF8Encoding(false, true)))
            .Select(json => (Json: json, Pack: new KnowledgePackLoader().LoadFromJson(json)))
            .ToDictionary(item => item.Pack.Manifest.PackId, StringComparer.Ordinal);
        var packs = catalog.ToDictionary(item => item.Key, item => item.Value.Pack, StringComparer.Ordinal);
        if (packs.Count == 0 || packs.Values.Any(pack => pack.Manifest.ValidationLevel != "SYNTHETIC"))
            throw new InvalidOperationException("Local demo catalog must contain synthetic knowledge only.");
        var presentations = PresentationCatalog.Load(packs);
        var platformVersion = typeof(DemoHost).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion;
        var app = builder.Build();
        var reviewCredential = app.Services.GetRequiredService<SyntheticReviewCredential>();
        var persistentReviewEnabled = app.Configuration.GetValue<bool>("SyntheticReview:PersistenceEnabled");
        if (persistentReviewEnabled && !reviewCredential.Enabled)
            throw new InvalidOperationException("Persistent synthetic review requires the verified review identity boundary.");

        if (persistentReviewEnabled)
        {
            var source = app.Services.GetRequiredService<Npgsql.NpgsqlDataSource>();
            new NormaCase.Persistence.PostgreSql.PostgresMigrationRunner(source).MigrateAsync().GetAwaiter().GetResult();
            var releases = new NormaCase.Persistence.PostgreSql.PostgresKnowledgeReleaseStore(source);
            foreach (var entry in catalog)
            {
                var retained = releases.RegisterAsync(entry.Value.Json).GetAwaiter().GetResult();
                var restored = releases.LoadAsync(retained.PackId, retained.ReleaseId).GetAwaiter().GetResult()
                    ?? throw new NormaCase.Persistence.PostgreSql.KnowledgeReleaseIntegrityException();
                packs[entry.Key] = restored.LoadPack();
            }
        }

        app.UseExceptionHandler(handler => handler.Run(async context =>
        {
            context.Response.StatusCode = 500;
            context.Response.Headers.CacheControl = "no-store";
            context.Response.Headers["X-Content-Type-Options"] = "nosniff";
            context.Response.Headers["Referrer-Policy"] = "no-referrer";
            context.Response.Headers["X-Frame-Options"] = "DENY";
            context.Response.Headers["Content-Security-Policy"] = "default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self'; connect-src 'self'; font-src 'self'; object-src 'none'; base-uri 'none'; frame-ancestors 'none'; form-action 'self'";
            await context.Response.WriteAsJsonAsync(new { code = "internal_error", message = ApiMessages.Get("internal_error") });
        }));
        app.Use(async (context, next) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            context.Response.Headers["X-Content-Type-Options"] = "nosniff";
            context.Response.Headers["Referrer-Policy"] = "no-referrer";
            context.Response.Headers["X-Frame-Options"] = "DENY";
            context.Response.Headers["Content-Security-Policy"] = "default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self'; connect-src 'self'; font-src 'self'; object-src 'none'; base-uri 'none'; frame-ancestors 'none'; form-action 'self'";
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

        app.UseAuthentication();
        app.UseAuthorization();
        if (reviewCredential.Enabled)
        {
            app.MapGet("/api/review-session", (System.Security.Claims.ClaimsPrincipal user) =>
                Results.Json(new { actorId = SyntheticReviewAuthentication.ResolveActor(user).ActorId }))
                .RequireAuthorization();
            if (persistentReviewEnabled)
            {
                SyntheticReviewEndpoints.Map(app, packs, platformVersion);
                SyntheticIdentityAdministrationEndpoints.Map(app);
            }
        }

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
                        workflows = presentation.Workflows,
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

            var pack = packs[packId];
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

        app.MapPost("/api/assessments/{packId}", (string packId, HttpRequest request) =>
        {
            if (!packs.TryGetValue(packId, out var pack))
                return Task.FromResult(Error("unknown_pack", 404));
            return HandleJson(request, json =>
            {
                var input = CaseInputJson.Deserialize(json);
                var result = new RuleEvaluator().Evaluate(pack, input.Facts, input.AssessmentDate, input.Evidence);
                return Results.Content(AssessmentJson.Serialize(result, platformVersion), "application/json", Encoding.UTF8);
            });
        });

        app.MapPost("/api/snapshots/{packId}", (string packId, HttpRequest request) =>
        {
            if (!catalog.TryGetValue(packId, out var item))
                return Task.FromResult(Error("unknown_pack", 404));
            return HandleJson(request, json =>
            {
                var snapshotJson = new AssessmentSnapshotService().Capture(item.Json, json, platformVersion);
                var snapshot = AssessmentSnapshotJson.Deserialize(snapshotJson);
                return Results.Json(new
                {
                    snapshotJson,
                    assessmentJson = AssessmentJson.Serialize(snapshot.Assessment.Assessment, platformVersion)
                });
            });
        });

        app.MapPost("/api/snapshots/replay", (HttpRequest request) => HandleJson(request, json =>
        {
            var snapshot = AssessmentSnapshotJson.Deserialize(json);
            var embedded = new KnowledgePackLoader().LoadFromJson(snapshot.KnowledgePackJson);
            if (embedded.Manifest.ValidationLevel != "SYNTHETIC")
                return Error("synthetic_only", 403);
            var result = new AssessmentSnapshotService().Replay(json, platformVersion);
            return Results.Json(new { assessmentJson = AssessmentJson.Serialize(result.Assessment, result.PlatformVersion) });
        }));

        WorkQueueEndpoints.Map(app, packs, platformVersion);
        WorkflowEndpoints.Map(app, packs, presentations, platformVersion);
        return app;
    }

    internal static async Task<IResult> HandleJson(HttpRequest request, Func<string, IResult> process)
    {
        if (!request.HasJsonContentType())
            return Error("json_required", 415);
        if (request.ContentLength > MaximumBodyBytes)
            return Error("input_too_large", 413);
        try
        {
            using var bytes = new MemoryStream();
            var buffer = new byte[4096];
            int read;
            while ((read = await request.Body.ReadAsync(buffer, request.HttpContext.RequestAborted)) > 0)
            {
                if (bytes.Length + read > MaximumBodyBytes)
                    return Error("input_too_large", 413);
                bytes.Write(buffer, 0, read);
            }
            var text = new UTF8Encoding(false, true).GetString(bytes.GetBuffer(), 0, checked((int)bytes.Length));
            return process(text.StartsWith('\uFEFF') ? text[1..] : text);
        }
        catch (BadHttpRequestException exception) when (exception.StatusCode == 413)
        {
            return Error("input_too_large", 413);
        }
        catch (SnapshotReplayException)
        {
            return Error("replay_mismatch", 409);
        }
        catch (NormaCase.Application.Workflows.WorkflowRunConcurrencyException)
        {
            return Error("workflow_revision", 409);
        }
        catch (NormaCase.Domain.Workflow.WorkflowTransitionNotAllowedException)
        {
            return Error("workflow_transition", 409);
        }
        catch (WorkflowCatalogMismatchException)
        {
            return Error("workflow_mismatch", 409);
        }
        catch (Exception exception) when (exception is JsonException or KnowledgeValidationException or ArgumentException or OverflowException)
        {
            return Error("invalid_input", 400);
        }
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

    internal static IResult Error(string code, int status)
        => Results.Json(new { code, message = ApiMessages.Get(code) }, statusCode: status);
}
