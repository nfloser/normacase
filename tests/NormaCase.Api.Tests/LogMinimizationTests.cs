using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using NormaCase.Api;
using Npgsql;
using Xunit;

namespace NormaCase.Api.Tests;

public sealed class LogMinimizationTests
{
    private const string Canary = "synthetic-case-secret-canary-302";
    private const string Token = "AAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh8=";

    private static WebApplicationFactory<Program> Host(CaptureLogs capture)
        => new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["SyntheticReview:Enabled"] = "true",
                    ["SyntheticReview:Credential"] = Token,
                    ["Logging:LogLevel:Default"] = "Trace",
                    ["Logging:LogLevel:Microsoft"] = "Trace"
                }));
            builder.ConfigureLogging(logging => logging.AddProvider(capture));
        });

    [Fact]
    public async Task Authentication_body_document_and_URL_canaries_are_absent_from_logs_and_errors()
    {
        using var capture = new CaptureLogs();
        await using var host = Host(capture);
        using var client = host.CreateClient();
        using var authentication = new HttpRequestMessage(HttpMethod.Get,
            "/api/review-session?access_token=" + Token + "&case=" + Canary);
        authentication.Headers.TryAddWithoutValidation("Authorization", "Bearer " + Canary);
        authentication.Headers.Add("X-Case", Canary);
        var denied = await client.SendAsync(authentication);
        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        Assert.DoesNotContain(Canary, await denied.Content.ReadAsStringAsync());
        Assert.DoesNotContain(Token, await denied.Content.ReadAsStringAsync());

        var invalid = await client.PostAsync("/api/assessments/synthetic.demo-a?case=" + Canary,
            new StringContent("{\"" + Canary + "\":", Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.DoesNotContain(Canary, await invalid.Content.ReadAsStringAsync());
        var unknown = await client.GetAsync("/api/document-cases/" + Canary + "/documents/" + Canary);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.DoesNotContain(Canary, await unknown.Content.ReadAsStringAsync());

        AssertSafe(capture, 401, 400, 404);
    }

    [Fact]
    public async Task Current_database_connection_failure_keeps_password_and_details_out_of_diagnostics()
    {
        using var capture = new CaptureLogs();
        await using var source = NpgsqlDataSource.Create(
            "Host=127.0.0.1;Port=1;Database=" + Canary + ";Username=" + Canary + ";Password=" + Canary + ";Timeout=1;Pooling=false");
        await using var host = Host(capture).WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<OperationalReadinessProbe>();
                services.AddSingleton(new OperationalReadinessProbe(source));
            }));
        using var client = host.CreateClient();
        var response = await client.GetAsync("/health/ready?connection=" + Canary);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.DoesNotContain(Canary, await response.Content.ReadAsStringAsync());
        AssertSafe(capture, 503);
    }

    [Fact]
    public async Task Framework_exceptions_scopes_and_outbound_failure_leave_only_bounded_status_events()
    {
        using var capture = new CaptureLogs();
        await using var host = Host(capture);
        using var client = host.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live")).StatusCode);
        var factory = host.Services.GetRequiredService<ILoggerFactory>();
        var framework = factory.CreateLogger("Microsoft.AspNetCore.Diagnostics.ExceptionHandlerMiddleware");
        using (framework.BeginScope("scope {Case}", Canary))
            framework.LogError(new IOException(Canary), "failure {Connection}", Canary);
        factory.CreateLogger("Npgsql.Connection").LogError(new IOException(Canary), "{Secret}", Canary);
        var context = new DefaultHttpContext();
        var safe = factory.CreateLogger(SafeDiagnostics.Category);
        await Assert.ThrowsAsync<IOException>(() => SafeDiagnostics.RecordAsync(context,
            _ => Task.FromException(new IOException("outbound " + Canary)), safe));
        AssertSafe(capture, 200, 500);
    }

    private static void AssertSafe(CaptureLogs capture, params int[] statuses)
    {
        Assert.NotEmpty(capture.Entries); // Prevent an empty logger from vacuously passing.
        foreach (var status in statuses)
            Assert.Contains(capture.Entries, entry => entry.Message == "NC_HTTP_COMPLETED " + status);
        Assert.All(capture.Entries, entry =>
        {
            Assert.Equal(SafeDiagnostics.Category, entry.Category);
            Assert.Equal(1000, entry.EventId);
            Assert.Null(entry.Exception);
            Assert.DoesNotContain(Canary, entry.Message + entry.State);
            Assert.DoesNotContain(Token, entry.Message + entry.State);
        });
    }

    private sealed class CaptureLogs : ILoggerProvider, ISupportExternalScope
    {
        internal ConcurrentQueue<Entry> Entries { get; } = new();
        internal IExternalScopeProvider Scopes { get; private set; } = new LoggerExternalScopeProvider();
        public void SetScopeProvider(IExternalScopeProvider scopeProvider) => Scopes = scopeProvider;
        public ILogger CreateLogger(string categoryName) => new CaptureLogger(categoryName, this);
        public void Dispose() { }
    }

    private sealed record Entry(string Category, int EventId, string Message, string State, string? Exception);

    private sealed class CaptureLogger(string category, CaptureLogs capture) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => capture.Scopes.Push(state);
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var scopes = new List<string>();
            capture.Scopes.ForEachScope((scope, values) => values.Add(scope?.ToString() ?? ""), scopes);
            capture.Entries.Enqueue(new(category, eventId.Id, formatter(state, exception),
                (state?.ToString() ?? "") + string.Join(";", scopes), exception?.ToString()));
        }
    }
}
