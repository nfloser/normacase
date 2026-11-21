using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using NormaCase.Api;
using Xunit;

namespace NormaCase.Api.Tests;

public sealed class DiagnosticSinkIsolationTests
{
    [Fact]
    public async Task Failing_sink_does_not_turn_a_successful_request_into_failure()
    {
        var context = new DefaultHttpContext();
        await SafeDiagnostics.RecordAsync(context, request =>
        {
            request.Response.StatusCode = StatusCodes.Status204NoContent;
            return Task.CompletedTask;
        }, new FailingLogger());
        Assert.Equal(StatusCodes.Status204NoContent, context.Response.StatusCode);
    }

    [Fact]
    public async Task Failing_sink_does_not_replace_the_original_delegate_exception()
    {
        var original = new IOException("synthetic-outbound-failure");
        var actual = await Assert.ThrowsAsync<IOException>(() => SafeDiagnostics.RecordAsync(
            new DefaultHttpContext(), _ => Task.FromException(original), new FailingLogger()));
        Assert.Same(original, actual);
    }

    private sealed class FailingLogger : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter)
            => throw new InvalidOperationException("synthetic-diagnostic-sink-failure");
    }
}
