using NormaCase.Persistence.PostgreSql;
using Npgsql;

namespace NormaCase.Api;

internal static class OperationalHealthEndpoints
{
    internal static void Map(WebApplication app)
    {
        app.MapGet("/health/live", () => Results.Json(new
        {
            status = ApiMessages.Get("health_live"),
            scope = ApiMessages.Get("health_scope")
        }));

        app.MapGet("/health/ready", async (
            OperationalReadinessProbe probe,
            CancellationToken cancellationToken) =>
        {
            var persistence = await probe.CheckAsync(cancellationToken);
            return Results.Json(new
            {
                status = ApiMessages.Get(persistence.Ready
                    ? "health_ready"
                    : "health_not_ready"),
                persistence = ApiMessages.Get(persistence.StateResource)
            }, statusCode: persistence.Ready ? StatusCodes.Status200OK : StatusCodes.Status503ServiceUnavailable);
        });
    }
}

internal sealed class OperationalReadinessProbe(NpgsqlDataSource? source)
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(2);

    internal async Task<OperationalReadiness> CheckAsync(CancellationToken cancellationToken)
    {
        if (source is null)
            return new(true, "health_persistence_disabled");

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(Timeout);
        try
        {
            var current = await new PostgresMigrationRunner(source)
                .IsCurrentAsync(timeout.Token);
            return current
                ? new(true, "health_persistence_available")
                : new(false, "health_persistence_unavailable");
        }
        catch (Exception exception) when (exception is NpgsqlException
            or IOException
            or InvalidOperationException
            or OperationCanceledException)
        {
            return new(false, "health_persistence_unavailable");
        }
    }
}

internal sealed record OperationalReadiness(bool Ready, string StateResource);
