using Microsoft.Extensions.Logging;

namespace NormaCase.Api;

internal static class SafeDiagnostics
{
    internal const string Category = "NormaCase.Api.SafeDiagnostics";
    private static readonly EventId Completed = new(1000, "HttpCompleted");

    internal static void Configure(ILoggingBuilder logging)
    {
        logging.ClearProviders();
        // Apply after configuration binding: Trace settings cannot enable request,
        // exception or database logging. Operators may supply a local sink only.
        logging.Services.Configure<LoggerFilterOptions>(options =>
        {
            options.Rules.Clear();
            options.Rules.Add(new LoggerFilterRule(null, null, LogLevel.Trace,
                (_, category, level) => category == Category && level == LogLevel.Information));
        });
    }

    internal static async Task RecordAsync(HttpContext context, RequestDelegate next, ILogger logger)
    {
        var status = StatusCodes.Status500InternalServerError;
        try
        {
            await next(context);
            status = context.Response.StatusCode;
        }
        finally
        {
            // Never pass HttpContext, URL, body, actor, exception, scope or request
            // identifiers to a logger. Clinical audit remains a separate boundary.
            if (status < 100 || status > 599) status = StatusCodes.Status500InternalServerError;
            try
            {
                logger.LogInformation(Completed, "NC_HTTP_COMPLETED {StatusCode}", status);
            }
            catch (Exception)
            {
                // Technical diagnostics are best effort. A failing operator sink
                // must not replace the request result or its original exception.
                // Durable clinical audit has its own transactional failure policy.
            }
        }
    }
}
