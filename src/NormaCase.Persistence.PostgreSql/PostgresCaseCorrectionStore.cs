using System.Text.Json;
using Npgsql;
using NpgsqlTypes;
using NormaCase.Application.Corrections;
using NormaCase.Application.Intake;
using NormaCase.Application.Reviews;
using NormaCase.Domain.Cases;
using NormaCase.Serialization;

namespace NormaCase.Persistence.PostgreSql;

public sealed partial class PostgresCaseReviewStore : ICaseCorrectionTransactionStore
{
    public async Task<CaseCorrectionPlan> ExecuteCorrectionAsync(CaseId caseId,
        Func<CaseReviewState, NormalizedIntakeRecord, CaseCorrectionPlan> prepare, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(prepare);
        var scope = PostgresAuthorizedOperation.Current
            ?? throw new CaseCorrectionDeniedException();
        if (!ReferenceEquals(scope.Source, source) || !scope.State.Actions.Contains("CORRECT", StringComparer.Ordinal)
            || !scope.State.Actions.Contains("READ", StringComparer.Ordinal)
            || !scope.State.CaseIds.Contains(caseId.Value, StringComparer.Ordinal))
            throw new CaseCorrectionDeniedException();
        try
        {
            var connection = scope.Connection; var transaction = scope.Transaction;
            // Intake always acquires the global stream lock before the case lock.
            // Preserve that order to avoid a correction/intake deadlock.
            await using (var streamLock = new NpgsqlCommand(
                "SELECT pg_advisory_xact_lock(hashtextextended('normacase.intake',0))", connection, transaction))
                await streamLock.ExecuteNonQueryAsync(token);
            await Lock(connection, transaction, caseId, token);
            var current = await Read(connection, transaction, caseId, token) ?? throw new CaseCorrectionBindingException();
            var intakeStore = new PostgresNormalizedIntakeStore(source);
            var original = await PostgresNormalizedIntakeStore.LoadForCaseOnConnectionAsync(connection, transaction, caseId, token) ?? throw new CaseCorrectionBindingException();
            var plan = prepare(current.State, original);
            if (plan.Link.ActorId != scope.State.ActorId || plan.Link.CaseId != caseId
                || plan.Next.Process.CaseId != caseId || plan.Input.CaseId != caseId)
                throw new CaseCorrectionBindingException();
            var json = CaseCorrectionLinkJson.Serialize(plan.Link);
            // A correction identity or new assessment cannot be reused for another append.
            await using (var identity = new NpgsqlCommand(
                "SELECT EXISTS(SELECT 1 FROM normacase.case_correction_links WHERE correction_id=$1 OR assessment_id=$2)",
                connection, transaction))
            {
                identity.Parameters.AddWithValue(plan.Link.CorrectionId);
                identity.Parameters.AddWithValue(plan.Next.Assessment.AssessmentId.Value);
                if ((bool)(await identity.ExecuteScalarAsync(token))!) throw new CaseCorrectionConflictException();
            }
            var receipt = await intakeStore.AppendAsync(plan.Input, token);
            if (receipt.Acceptance != IntakeAcceptance.Accepted) throw new CaseCorrectionConflictException();
            await PostgresAssessmentRecordStore.AppendOnConnectionAsync(plan.Next.Assessment, connection, transaction, token);
            var version = checked(current.Version + 1);
            await Insert(connection, transaction, plan.Next, version, CaseReviewStateJson.Serialize(plan.Next), token);
            await using (var insert = new NpgsqlCommand("""
                INSERT INTO normacase.case_correction_links
                    (correction_id,case_id,review_version,previous_assessment_id,assessment_id,link_json,link_sha256)
                VALUES($1,$2,$3,$4,$5,$6,$7);
                """, connection, transaction))
            {
                insert.Parameters.AddWithValue(plan.Link.CorrectionId); insert.Parameters.AddWithValue(caseId.Value);
                insert.Parameters.AddWithValue(version); insert.Parameters.AddWithValue(plan.Link.PreviousAssessmentId.Value);
                insert.Parameters.AddWithValue(plan.Link.AssessmentId.Value);
                insert.Parameters.AddWithValue(NpgsqlDbType.Json, json); insert.Parameters.AddWithValue(Hash(json));
                await insert.ExecuteNonQueryAsync(token);
            }
            await VerifyCorrection(connection, transaction, current.State, plan.Next, version, token);
            token.ThrowIfCancellationRequested();
            return plan; // Outer live authorization scope is the sole commit owner.
        }
        catch (PostgresException exception) when (exception.SqlState == "23505") { throw new CaseCorrectionConflictException(); }
        catch (NpgsqlException) { throw new CaseReviewStorageException(); }
    }

    public async Task<IReadOnlyList<CaseReviewHistoryEntry>> LoadHistoryPageAsync(CaseId caseId,
        int pageSize, long? afterVersion, CancellationToken token = default)
    {
        if (caseId.IsEmpty || pageSize is < 1 or > 100 || afterVersion is < 0)
            throw new ArgumentException("Invalid case history page.");
        try
        {
            await using var session = await PostgresOperationSession.OpenAsync(source, token);
            await Lock(session.Connection, session.Transaction, caseId, token);
            // Verify the complete chain before exposing any bounded page of snapshots.
            await Read(session.Connection, session.Transaction, caseId, token);
            var entries = new List<CaseReviewHistoryEntry>();
            var rows = new List<(long Version, string Json)>();
            await using (var query = new NpgsqlCommand(
                "SELECT version,state_json::text FROM normacase.case_review_versions WHERE case_id=$1 AND ($2::bigint IS NULL OR version>$2) ORDER BY version LIMIT $3",
                session.Connection, session.Transaction))
            {
                query.Parameters.AddWithValue(caseId.Value);
                query.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Bigint, Value = (object?)afterVersion ?? DBNull.Value });
                query.Parameters.AddWithValue(pageSize + 1);
                await using var reader = await query.ExecuteReaderAsync(token);
                while (await reader.ReadAsync(token)) rows.Add((reader.GetInt64(0), reader.GetString(1)));
            }
            foreach (var row in rows)
                entries.Add(new(row.Version, CaseReviewStateJson.Deserialize(row.Json, resolver),
                    await ReadCorrection(session.Connection, session.Transaction, caseId, row.Version, token)));
            return entries.AsReadOnly();
        }
        catch (NpgsqlException) { throw new CaseReviewStorageException(); }
    }

    public async Task<CaseReviewHistoryEntry?> LoadRevisionAsync(CaseId caseId, long version, CancellationToken token = default)
    {
        if (caseId.IsEmpty || version < 0) throw new ArgumentException("Explicit case history version required.");
        try
        {
            await using var session = await PostgresOperationSession.OpenAsync(source, token);
            await Lock(session.Connection, session.Transaction, caseId, token);
            await Read(session.Connection, session.Transaction, caseId, token);
            string? json;
            await using (var query = new NpgsqlCommand(
                "SELECT state_json::text FROM normacase.case_review_versions WHERE case_id=$1 AND version=$2",
                session.Connection, session.Transaction))
            {
                query.Parameters.AddWithValue(caseId.Value); query.Parameters.AddWithValue(version);
                json = (string?)await query.ExecuteScalarAsync(token);
            }
            return json is null ? null : new(version, CaseReviewStateJson.Deserialize(json, resolver),
                await ReadCorrection(session.Connection, session.Transaction, caseId, version, token));
        }
        catch (NpgsqlException) { throw new CaseReviewStorageException(); }
    }

    private async Task VerifyCorrection(NpgsqlConnection connection, NpgsqlTransaction transaction,
        CaseReviewState previous, CaseReviewState next, long version, CancellationToken token)
    {
        var link = await ReadCorrection(connection, transaction, next.Process.CaseId, version, token)
            ?? throw new CaseReviewIntegrityException();
        if (link.CaseId != previous.Process.CaseId || link.CaseId != next.Process.CaseId
            || link.PreviousAssessmentId != previous.Assessment.AssessmentId || link.AssessmentId != next.Assessment.AssessmentId
            || link.PreviousCaseRevision != previous.Process.CaseRevision || link.CaseRevision != next.Process.CaseRevision
            || link.PreviousProcessRevision != previous.Process.Revision || link.PreviousAuditRevision != previous.Audit.Events[^1].Sequence
            || previous.Process.WorkflowId != next.Process.WorkflowId || previous.Process.WorkflowVersion != next.Process.WorkflowVersion
            || next.Process.Revision != 1
            || next.AssessmentCaseRevision != next.Process.CaseRevision
            || next.Audit.Events.Count != 1 || next.Audit.Events[0].OccurredAt != link.RecordedAtUtc
            || next.Assessment.RecordedAtUtc != link.RecordedAtUtc || link.RecordedAtUtc < previous.Audit.Events[^1].OccurredAt)
            throw new CaseReviewIntegrityException();
        var definition = resolver(next.Process.WorkflowId, next.Process.WorkflowVersion);
        if (!definition.Transitions.Any(t => t.FromStateId == definition.InitialStateId && t.ToStateId == next.Process.StateId))
            throw new CaseReviewIntegrityException();
        var original = await PostgresNormalizedIntakeStore.Read(connection, transaction, link.SourceSystemId, link.UpstreamCaseId, link.PreviousCaseRevision, token)
            ?? throw new CaseReviewIntegrityException();
        var corrected = await PostgresNormalizedIntakeStore.Read(connection, transaction, link.SourceSystemId, link.UpstreamCaseId, link.CaseRevision, token)
            ?? throw new CaseReviewIntegrityException();
        if (original.CaseId != link.CaseId || corrected.CaseId != link.CaseId || original.CaseTypeId != corrected.CaseTypeId
            || corrected.KnowledgePackId != original.KnowledgePackId || corrected.KnowledgeRelease != original.KnowledgeRelease
            || corrected.Provenance.ReceivedAtUtc < original.Provenance.ReceivedAtUtc || corrected.Provenance.ReceivedAtUtc > link.RecordedAtUtc
            || original.Provenance.MessageId != link.PreviousMessageId || corrected.Provenance.MessageId != link.MessageId
            || original.KnowledgePackId != previous.Assessment.KnowledgePackId || original.KnowledgeRelease != previous.Assessment.Result.KnowledgeRelease
            || corrected.KnowledgePackId != next.Assessment.KnowledgePackId || corrected.KnowledgeRelease != next.Assessment.Result.KnowledgeRelease
            || !SameInput(original, previous) || !SameInput(corrected, next))
            throw new CaseReviewIntegrityException();
        await VerifyAssessment(connection, transaction, next, token);
    }

    private static bool SameInput(NormalizedIntakeRecord intake, CaseReviewState state)
        => intake.Input.AssessmentDate == state.Assessment.Input.AssessmentDate
            && intake.Input.Facts.Count == state.Assessment.Input.Facts.Count
            && intake.Input.Facts.All(item => state.Assessment.Input.Facts.TryGetValue(item.Key, out var value) && value.Equals(item.Value))
            && intake.Input.Evidence.Count == state.Assessment.Input.Evidence.Count
            && intake.Input.Evidence.All(item => state.Assessment.Input.Evidence.TryGetValue(item.Key, out var value) && value == item.Value);

    private static async Task<CaseCorrectionLink?> ReadCorrection(NpgsqlConnection connection, NpgsqlTransaction transaction,
        CaseId caseId, long version, CancellationToken token)
    {
        await using var query = new NpgsqlCommand("""
            SELECT correction_id,previous_assessment_id,assessment_id,link_json::text,link_sha256
            FROM normacase.case_correction_links WHERE case_id=$1 AND review_version=$2;
            """, connection, transaction);
        query.Parameters.AddWithValue(caseId.Value); query.Parameters.AddWithValue(version);
        await using var reader = await query.ExecuteReaderAsync(token);
        if (!await reader.ReadAsync(token)) return null;
        var json = reader.GetString(3);
        if (Hash(json) != reader.GetString(4)) throw new CaseReviewIntegrityException();
        try
        {
            var link = CaseCorrectionLinkJson.Deserialize(json);
            if (link.CaseId != caseId || link.CorrectionId != reader.GetString(0)
                || link.PreviousAssessmentId.Value != reader.GetString(1) || link.AssessmentId.Value != reader.GetString(2))
                throw new CaseReviewIntegrityException();
            return link;
        }
        catch (Exception exception) when (exception is ArgumentException or JsonException) { throw new CaseReviewIntegrityException(); }
    }
}

public sealed record CaseReviewHistoryEntry(long Version, CaseReviewState State, CaseCorrectionLink? Correction);
