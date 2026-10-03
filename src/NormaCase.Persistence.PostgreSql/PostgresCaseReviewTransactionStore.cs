using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Npgsql;
using NpgsqlTypes;
using NormaCase.Application.Assessments;
using NormaCase.Application.Reviews;
using NormaCase.Domain.Audit;
using NormaCase.Domain.Cases;
using NormaCase.Domain.Workflow;
using NormaCase.Serialization;

namespace NormaCase.Persistence.PostgreSql;

public sealed class PostgresCaseReviewTransactionStore
    : ICaseReviewTransactionStore
{
    private readonly NpgsqlDataSource dataSource;
    private readonly PostgresAssessmentRecordStore assessmentRecords;

    public PostgresCaseReviewTransactionStore(
        NpgsqlDataSource dataSource)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        this.dataSource = dataSource;
        assessmentRecords = new(dataSource);
    }

    public async Task InitializeAsync(
        AssessmentRecord assessment,
        long assessmentCaseRevision,
        CaseProcessingInstance process,
        WorkflowDefinition definition,
        AssessmentAuditTrail audit,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(assessment);
        if (assessmentCaseRevision < 1)
            throw new ArgumentOutOfRangeException(nameof(assessmentCaseRevision));
        ArgumentNullException.ThrowIfNull(process);
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(audit);

        ValidateBinding(
            assessment,
            assessmentCaseRevision,
            process,
            definition,
            audit,
            requireCreationOnly: true);

        var persisted = await assessmentRecords.LoadAsync(
            assessment.AssessmentId,
            cancellationToken);

        if (persisted is null
            || !string.Equals(
                AssessmentRecordJson.Serialize(persisted),
                AssessmentRecordJson.Serialize(assessment),
                StringComparison.Ordinal))
        {
            throw new CaseReviewAggregateBindingException();
        }

        var processJson = CaseProcessingJson.Serialize(
            process,
            definition);
        var auditJson = AssessmentAuditJson.Serialize(audit);

        try
        {
            await using var connection =
                await dataSource.OpenConnectionAsync(cancellationToken);
            await using var transaction =
                await connection.BeginTransactionAsync(cancellationToken);

            await LockCaseAsync(
                connection,
                transaction,
                assessment.CaseId,
                cancellationToken);

            var latest = await ReadLatestAsync(
                connection,
                transaction,
                assessment.CaseId,
                forUpdate: true,
                cancellationToken);

            if (latest is not null
                && latest.AssessmentCaseRevision >= assessmentCaseRevision)
            {
                throw new CaseReviewAggregateConflictException();
            }

            await InsertAsync(
                connection,
                transaction,
                assessment.AssessmentId,
                assessmentCaseRevision,
                process,
                processJson,
                audit,
                auditJson,
                cancellationToken);

            await transaction.CommitAsync(cancellationToken);
        }
        catch (CaseReviewAggregateConflictException)
        {
            throw;
        }
        catch (PostgresException exception)
            when (exception.SqlState
                is PostgresErrorCodes.UniqueViolation
                or PostgresErrorCodes.ForeignKeyViolation)
        {
            throw new CaseReviewAggregateConflictException();
        }
        catch (NpgsqlException)
        {
            throw new CaseReviewAggregateStorageException();
        }
    }

    public async Task<CaseReviewState?> LoadLatestAsync(
        CaseId caseId,
        CancellationToken cancellationToken = default)
    {
        if (caseId.IsEmpty)
            throw new ArgumentException(
                "Case id must be explicit.",
                nameof(caseId));

        try
        {
            await using var connection =
                await dataSource.OpenConnectionAsync(cancellationToken);
            var stored = await ReadLatestAsync(
                connection,
                transaction: null,
                caseId,
                forUpdate: false,
                cancellationToken);

            return stored is null
                ? null
                : await VerifyAsync(
                    stored,
                    cancellationToken);
        }
        catch (CaseReviewAggregateIntegrityException)
        {
            throw;
        }
        catch (NpgsqlException)
        {
            throw new CaseReviewAggregateStorageException();
        }
    }

    public async Task<CaseReviewState> ExecuteAsync(
        CaseId caseId,
        Func<CaseReviewState, CaseReviewState> update,
        CancellationToken cancellationToken = default)
    {
        if (caseId.IsEmpty)
            throw new ArgumentException(
                "Case id must be explicit.",
                nameof(caseId));
        ArgumentNullException.ThrowIfNull(update);

        try
        {
            await using var connection =
                await dataSource.OpenConnectionAsync(cancellationToken);
            await using var transaction =
                await connection.BeginTransactionAsync(cancellationToken);

            await LockCaseAsync(
                connection,
                transaction,
                caseId,
                cancellationToken);

            var stored = await ReadLatestAsync(
                connection,
                transaction,
                caseId,
                forUpdate: true,
                cancellationToken)
                ?? throw new CaseReviewAggregateNotFoundException();

            var current = await VerifyAsync(
                stored,
                cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();
            var next = update(current);
            ArgumentNullException.ThrowIfNull(next);
            cancellationToken.ThrowIfCancellationRequested();

            var snapshot = CaseProcessingJson.Deserialize(
                stored.ProcessJson);
            ValidateSuccessor(
                current,
                next,
                snapshot.Definition);

            var processJson = CaseProcessingJson.Serialize(
                next.Process,
                snapshot.Definition);
            var auditJson = AssessmentAuditJson.Serialize(
                next.Audit);

            await InsertAsync(
                connection,
                transaction,
                next.Assessment.AssessmentId,
                next.AssessmentCaseRevision,
                next.Process,
                processJson,
                next.Audit,
                auditJson,
                cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();
            await transaction.CommitAsync(cancellationToken);
            return next;
        }
        catch (CaseReviewAggregateNotFoundException)
        {
            throw;
        }
        catch (CaseReviewAggregateIntegrityException)
        {
            throw;
        }
        catch (CaseReviewAggregateConflictException)
        {
            throw;
        }
        catch (PostgresException exception)
            when (exception.SqlState
                is PostgresErrorCodes.UniqueViolation
                or PostgresErrorCodes.ForeignKeyViolation)
        {
            throw new CaseReviewAggregateConflictException();
        }
        catch (PostgresException)
        {
            throw new CaseReviewAggregateStorageException();
        }
        catch (NpgsqlException)
        {
            throw new CaseReviewAggregateStorageException();
        }
    }

    private async Task<CaseReviewState> VerifyAsync(
        StoredAggregate stored,
        CancellationToken cancellationToken)
    {
        if (stored.ProcessFormatVersion
                != CaseProcessingJson.CurrentFormatVersion
            || stored.AuditFormatVersion
                != AssessmentAuditJson.CurrentFormatVersion
            || !FixedTimeHashEquals(
                stored.ProcessChecksum,
                Sha256(stored.ProcessJson))
            || !FixedTimeHashEquals(
                stored.AuditChecksum,
                Sha256(stored.AuditJson)))
        {
            throw new CaseReviewAggregateIntegrityException();
        }

        CaseProcessingSnapshot process;
        AssessmentAuditTrail audit;
        try
        {
            process = CaseProcessingJson.Deserialize(
                stored.ProcessJson);
            audit = AssessmentAuditJson.Deserialize(
                stored.AuditJson);
        }
        catch (JsonException)
        {
            throw new CaseReviewAggregateIntegrityException();
        }

        var assessment = await assessmentRecords.LoadAsync(
            new AssessmentId(stored.AssessmentId),
            cancellationToken);

        if (assessment is null)
            throw new CaseReviewAggregateIntegrityException();

        if (!string.Equals(
                process.Process.CaseId.Value,
                stored.CaseId,
                StringComparison.Ordinal)
            || process.Process.CaseRevision
                != stored.AssessmentCaseRevision
            || process.Process.Revision
                != stored.ProcessRevision
            || !string.Equals(
                process.Process.WorkflowId,
                stored.WorkflowId,
                StringComparison.Ordinal)
            || process.Process.WorkflowVersion
                != stored.WorkflowVersion
            || !string.Equals(
                process.Process.StateId,
                stored.StateId,
                StringComparison.Ordinal)
            || audit.AssessmentId != assessment.AssessmentId
            || audit.Events[^1].Sequence
                != stored.AuditLastSequence)
        {
            throw new CaseReviewAggregateIntegrityException();
        }

        try
        {
            ValidateBinding(
                assessment,
                stored.AssessmentCaseRevision,
                process.Process,
                process.Definition,
                audit,
                requireCreationOnly: false);
        }
        catch (ArgumentException)
        {
            throw new CaseReviewAggregateIntegrityException();
        }

        return new(
            assessment,
            stored.AssessmentCaseRevision,
            process.Process,
            audit);
    }

    private static void ValidateBinding(
        AssessmentRecord assessment,
        long assessmentCaseRevision,
        CaseProcessingInstance process,
        WorkflowDefinition definition,
        AssessmentAuditTrail audit,
        bool requireCreationOnly)
    {
        _ = process.IsTerminal(definition);

        var created = audit.Events[0];
        if (assessment.CaseId != process.CaseId
            || assessmentCaseRevision != process.CaseRevision
            || audit.AssessmentId != assessment.AssessmentId
            || created.Kind != AuditEventKind.AssessmentCreated
            || created.AssessmentId != assessment.AssessmentId
            || created.OccurredAt != assessment.RecordedAtUtc
            || (requireCreationOnly && audit.Events.Count != 1))
        {
            throw new CaseReviewAggregateBindingException();
        }
    }

    private static void ValidateSuccessor(
        CaseReviewState current,
        CaseReviewState next,
        WorkflowDefinition definition)
    {
        if (!string.Equals(
                AssessmentRecordJson.Serialize(current.Assessment),
                AssessmentRecordJson.Serialize(next.Assessment),
                StringComparison.Ordinal)
            || next.AssessmentCaseRevision
                != current.AssessmentCaseRevision
            || next.Process.CaseId
                != current.Process.CaseId
            || next.Process.CaseRevision
                != current.Process.CaseRevision
            || !string.Equals(
                next.Process.WorkflowId,
                current.Process.WorkflowId,
                StringComparison.Ordinal)
            || next.Process.WorkflowVersion
                != current.Process.WorkflowVersion
            || next.Process.Revision
                != checked(current.Process.Revision + 1)
            || next.Audit.AssessmentId
                != current.Audit.AssessmentId
            || next.Audit.Events.Count
                != current.Audit.Events.Count + 1)
        {
            throw new CaseReviewAggregateConflictException();
        }

        var prefix = Prefix(
            next.Audit,
            current.Audit.Events.Count);

        if (!string.Equals(
                AssessmentAuditJson.Serialize(prefix),
                AssessmentAuditJson.Serialize(current.Audit),
                StringComparison.Ordinal))
        {
            throw new CaseReviewAggregateConflictException();
        }

        var reachable = definition.Transitions.Any(
            transition =>
                string.Equals(
                    transition.FromStateId,
                    current.Process.StateId,
                    StringComparison.Ordinal)
                && string.Equals(
                    transition.ToStateId,
                    next.Process.StateId,
                    StringComparison.Ordinal));

        if (!reachable)
            throw new CaseReviewAggregateConflictException();

        _ = next.Process.IsTerminal(definition);
    }

    private static AssessmentAuditTrail Prefix(
        AssessmentAuditTrail trail,
        int eventCount)
    {
        var prefix = AssessmentAuditTrail.Start(
            trail.Events[0]);

        for (var index = 1; index < eventCount; index++)
            prefix = prefix.Append(trail.Events[index]);

        return prefix;
    }

    private static async Task LockCaseAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CaseId caseId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "SELECT pg_advisory_xact_lock(hashtextextended($1, 136));",
            connection,
            transaction);
        command.Parameters.AddWithValue(caseId.Value);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<StoredAggregate?> ReadLatestAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        CaseId caseId,
        bool forUpdate,
        CancellationToken cancellationToken)
    {
        var sql =
            """
            SELECT
                case_id,
                assessment_case_revision,
                process_revision,
                assessment_id,
                workflow_id,
                workflow_version,
                state_id,
                audit_last_sequence,
                process_format_version,
                process_json::text,
                process_sha256,
                audit_format_version,
                audit_json::text,
                audit_sha256
            FROM normacase.case_review_aggregate_versions
            WHERE case_id = $1
            ORDER BY
                assessment_case_revision DESC,
                process_revision DESC
            LIMIT 1
            """
            + (forUpdate ? " FOR UPDATE;" : ";");

        await using var command = new NpgsqlCommand(
            sql,
            connection,
            transaction);
        command.Parameters.AddWithValue(caseId.Value);

        await using var reader =
            await command.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return new(
            reader.GetString(0),
            reader.GetInt64(1),
            reader.GetInt64(2),
            reader.GetString(3),
            reader.GetString(4),
            reader.GetInt32(5),
            reader.GetString(6),
            reader.GetInt64(7),
            reader.GetInt32(8),
            reader.GetString(9),
            reader.GetString(10),
            reader.GetInt32(11),
            reader.GetString(12),
            reader.GetString(13));
    }

    private static async Task InsertAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        AssessmentId assessmentId,
        long assessmentCaseRevision,
        CaseProcessingInstance process,
        string processJson,
        AssessmentAuditTrail audit,
        string auditJson,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO normacase.case_review_aggregate_versions (
                case_id,
                assessment_case_revision,
                process_revision,
                assessment_id,
                workflow_id,
                workflow_version,
                state_id,
                audit_last_sequence,
                process_format_version,
                process_json,
                process_sha256,
                audit_format_version,
                audit_json,
                audit_sha256
            )
            VALUES (
                $1, $2, $3, $4, $5, $6, $7,
                $8, $9, $10, $11, $12, $13, $14
            );
            """,
            connection,
            transaction);

        command.Parameters.AddWithValue(process.CaseId.Value);
        command.Parameters.AddWithValue(assessmentCaseRevision);
        command.Parameters.AddWithValue(process.Revision);
        command.Parameters.AddWithValue(assessmentId.Value);
        command.Parameters.AddWithValue(process.WorkflowId);
        command.Parameters.AddWithValue(process.WorkflowVersion);
        command.Parameters.AddWithValue(process.StateId);
        command.Parameters.AddWithValue(audit.Events[^1].Sequence);
        command.Parameters.AddWithValue(
            CaseProcessingJson.CurrentFormatVersion);
        command.Parameters.AddWithValue(
            NpgsqlDbType.Json,
            processJson);
        command.Parameters.AddWithValue(Sha256(processJson));
        command.Parameters.AddWithValue(
            AssessmentAuditJson.CurrentFormatVersion);
        command.Parameters.AddWithValue(
            NpgsqlDbType.Json,
            auditJson);
        command.Parameters.AddWithValue(Sha256(auditJson));

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static bool FixedTimeHashEquals(
        string left,
        string right)
    {
        var leftBytes = Encoding.ASCII.GetBytes(left);
        var rightBytes = Encoding.ASCII.GetBytes(right);
        return CryptographicOperations.FixedTimeEquals(
            leftBytes,
            rightBytes);
    }

    private static string Sha256(string json)
        => Convert.ToHexString(
                SHA256.HashData(
                    Encoding.UTF8.GetBytes(json)))
            .ToLowerInvariant();

    private sealed record StoredAggregate(
        string CaseId,
        long AssessmentCaseRevision,
        long ProcessRevision,
        string AssessmentId,
        string WorkflowId,
        int WorkflowVersion,
        string StateId,
        long AuditLastSequence,
        int ProcessFormatVersion,
        string ProcessJson,
        string ProcessChecksum,
        int AuditFormatVersion,
        string AuditJson,
        string AuditChecksum);
}

public sealed class CaseReviewAggregateNotFoundException
    : Exception
{
    public CaseReviewAggregateNotFoundException()
        : base("Case review aggregate does not exist.") { }
}

public sealed class CaseReviewAggregateConflictException
    : Exception
{
    public CaseReviewAggregateConflictException()
        : base("Case review aggregate cannot be appended.") { }
}

public sealed class CaseReviewAggregateBindingException
    : Exception
{
    public CaseReviewAggregateBindingException()
        : base("Case review aggregate does not match the immutable assessment binding.") { }
}

public sealed class CaseReviewAggregateIntegrityException
    : Exception
{
    public CaseReviewAggregateIntegrityException()
        : base("Stored case review aggregate failed integrity verification.") { }
}

public sealed class CaseReviewAggregateStorageException
    : Exception
{
    public CaseReviewAggregateStorageException()
        : base("Case review aggregate storage operation failed.") { }
}
