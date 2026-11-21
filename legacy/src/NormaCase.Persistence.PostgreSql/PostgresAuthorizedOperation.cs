using Npgsql;
using NormaCase.Application.Authorization;

namespace NormaCase.Persistence.PostgreSql;

// Infrastructure-only ambient scope. All protected stores borrow the exact transaction
// which owns the entitlement lock; they must never dispose or commit it themselves.
internal static class PostgresAuthorizedOperation
{
    private static readonly AsyncLocal<Context?> CurrentContext = new();
    internal sealed record Context(NpgsqlDataSource Source, NpgsqlConnection Connection,
        NpgsqlTransaction Transaction, IdentityEntitlementState State);
    internal static Context? Current => CurrentContext.Value;

    internal static async Task<T> RunAsync<T>(Context context, Func<Task<T>> action)
    {
        if (Current is not null) throw new InvalidOperationException("Nested authorized operations are not supported.");
        CurrentContext.Value = context;
        try { return await action(); }
        finally { CurrentContext.Value = null; }
    }
}

internal sealed class PostgresOperationSession : IAsyncDisposable
{
    internal NpgsqlConnection Connection { get; }
    internal NpgsqlTransaction Transaction { get; }
    private readonly bool owns;

    private PostgresOperationSession(NpgsqlConnection connection, NpgsqlTransaction transaction, bool owns)
        => (Connection, Transaction, this.owns) = (connection, transaction, owns);

    internal static async Task<PostgresOperationSession> OpenAsync(NpgsqlDataSource source, CancellationToken token)
    {
        if (PostgresAuthorizedOperation.Current is { } current)
        {
            if (!ReferenceEquals(source, current.Source))
                throw new InvalidOperationException("Authorized operation cannot cross data sources.");
            // A lost authorization connection must never be replaced by a fresh connection.
            // The next database command (and the outer commit) will fail on this same backend.
            return new(current.Connection, current.Transaction, false);
        }
        var connection = await source.OpenConnectionAsync(token);
        try { return new(connection, await connection.BeginTransactionAsync(token), true); }
        catch { await connection.DisposeAsync(); throw; }
    }

    internal Task CommitAsync(CancellationToken token)
        => owns ? Transaction.CommitAsync(token) : Task.CompletedTask;

    public async ValueTask DisposeAsync()
    {
        if (!owns) return;
        try { await Transaction.DisposeAsync(); }
        finally { await Connection.DisposeAsync(); }
    }
}
