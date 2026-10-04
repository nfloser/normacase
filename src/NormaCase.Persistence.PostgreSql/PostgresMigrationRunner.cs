using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Npgsql;

namespace NormaCase.Persistence.PostgreSql;

public sealed class PostgresMigrationRunner
{
    private const string ResourcePrefix =
        "NormaCase.Persistence.PostgreSql.Migrations.";
    private const string LegacySchemaBootstrap =
        "CREATE SCHEMA IF NOT EXISTS normacase;";

    private readonly NpgsqlDataSource _dataSource;

    public PostgresMigrationRunner(NpgsqlDataSource dataSource)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        _dataSource = dataSource;
    }

    public async Task MigrateAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection =
                await _dataSource.OpenConnectionAsync(cancellationToken);
            await using var transaction =
                await connection.BeginTransactionAsync(cancellationToken);

            await using (var schemaLookup = new NpgsqlCommand(
                """
                SELECT 1
                FROM pg_namespace
                WHERE nspname = 'normacase';
                """,
                connection,
                transaction))
            {
                if (await schemaLookup.ExecuteScalarAsync(cancellationToken) is null)
                {
                    await using var createSchema = new NpgsqlCommand(
                        "CREATE SCHEMA normacase;",
                        connection,
                        transaction);
                    await createSchema.ExecuteNonQueryAsync(cancellationToken);
                }
            }

            await using (var bootstrap = new NpgsqlCommand(
                """
                CREATE TABLE IF NOT EXISTS normacase.schema_migrations (
                    version integer PRIMARY KEY,
                    checksum text NOT NULL
                        CHECK (checksum ~ '^[0-9a-f]{64}$')
                );
                """,
                connection,
                transaction))
            {
                await bootstrap.ExecuteNonQueryAsync(cancellationToken);
            }

            foreach (var migration in LoadMigrations())
            {
                await ApplyAsync(
                    connection,
                    transaction,
                    migration,
                    cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch (PostgresMigrationException)
        {
            throw;
        }
        catch (Exception exception)
            when (exception is NpgsqlException
                or IOException
                or InvalidOperationException)
        {
            throw new PostgresMigrationException();
        }
    }

    public async Task<bool> IsCurrentAsync(
        CancellationToken cancellationToken = default)
    {
        var expected = LoadMigrations();
        await using var connection =
            await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(
            """
            SELECT version, checksum
            FROM normacase.schema_migrations
            ORDER BY version;
            """,
            connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var index = 0;
        while (await reader.ReadAsync(cancellationToken))
        {
            if (index >= expected.Count
                || reader.GetInt32(0) != expected[index].Version
                || !string.Equals(
                    reader.GetString(1),
                    expected[index].Checksum,
                    StringComparison.Ordinal))
            {
                return false;
            }

            index++;
        }

        return index == expected.Count;
    }

    private static async Task ApplyAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Migration migration,
        CancellationToken cancellationToken)
    {
        await using var lookup = new NpgsqlCommand(
            """
            SELECT checksum
            FROM normacase.schema_migrations
            WHERE version = $1;
            """,
            connection,
            transaction);
        lookup.Parameters.AddWithValue(migration.Version);

        var existing = await lookup.ExecuteScalarAsync(cancellationToken);
        if (existing is string checksum)
        {
            if (!string.Equals(
                checksum,
                migration.Checksum,
                StringComparison.Ordinal))
            {
                throw new PostgresMigrationException();
            }

            return;
        }

        await using (var command = new NpgsqlCommand(
            ExecutableSql(migration),
            connection,
            transaction))
        {
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await using var insert = new NpgsqlCommand(
            """
            INSERT INTO normacase.schema_migrations(version, checksum)
            VALUES ($1, $2);
            """,
            connection,
            transaction);
        insert.Parameters.AddWithValue(migration.Version);
        insert.Parameters.AddWithValue(migration.Checksum);
        await insert.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string ExecutableSql(Migration migration)
    {
        // Migration 001 predates the separately provisioned schema owner and repeats
        // the runner bootstrap. Keep its original bytes/checksum for existing ledgers,
        // but do not require database-wide CREATE after the schema is already present.
        if (migration.Version == 1
            && migration.Sql.StartsWith(LegacySchemaBootstrap, StringComparison.Ordinal))
        {
            return migration.Sql[LegacySchemaBootstrap.Length..];
        }

        return migration.Sql;
    }

    private static IReadOnlyList<Migration> LoadMigrations()
    {
        var assembly = typeof(PostgresMigrationRunner).Assembly;
        var migrations = assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith(
                ResourcePrefix,
                StringComparison.Ordinal))
            .OrderBy(name => name, StringComparer.Ordinal)
            .Select(name => Load(assembly, name))
            .ToArray();

        if (migrations.Length == 0)
            throw new PostgresMigrationException();

        return migrations;
    }

    private static Migration Load(
        Assembly assembly,
        string resourceName)
    {
        var fileName = resourceName[ResourcePrefix.Length..];
        var separator = fileName.IndexOf('_');
        if (separator <= 0
            || !int.TryParse(
                fileName[..separator],
                out var version))
        {
            throw new PostgresMigrationException();
        }

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new PostgresMigrationException();
        using var reader = new StreamReader(
            stream,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true);
        var sql = reader.ReadToEnd();
        var checksum = Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(sql)))
            .ToLowerInvariant();

        return new(version, sql, checksum);
    }

    private sealed record Migration(
        int Version,
        string Sql,
        string Checksum);
}

public sealed class PostgresMigrationException : Exception
{
    public PostgresMigrationException()
        : base("PostgreSQL schema migration failed.")
    {
    }
}
