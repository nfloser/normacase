using System.Text.Json;
using Npgsql;
using NpgsqlTypes;
using NormaCase.Application.Corrections;
using NormaCase.Application.Reviews;
using NormaCase.Domain.Cases;
using NormaCase.Serialization;

namespace NormaCase.Persistence.PostgreSql;

public sealed record CaseClarificationEntry(CaseClarificationRecord Request, string? ResolvedByCorrectionId);
public sealed partial class PostgresCaseReviewStore
{
    public async Task<CaseClarificationRecord> RequestClarificationAsync(CaseId caseId,
        Func<CaseReviewState, CaseClarificationRecord> prepare, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(prepare);
        var scope = ClarificationScope(caseId, "CLARIFY");
        try
        {
            await Lock(scope.Connection, scope.Transaction, caseId, token);
            var current = await Read(scope.Connection, scope.Transaction, caseId, token) ?? throw new CaseCorrectionBindingException();
            var record = prepare(current.State);
            if (record.ActorId != scope.State.ActorId || !Matches(record, current.State))
                throw new CaseCorrectionBindingException();
            var json = CaseClarificationJson.Serialize(record);
            await using var insert = new NpgsqlCommand("""
                INSERT INTO normacase.case_clarification_requests(clarification_id,case_id,review_version,request_json,request_sha256)
                VALUES($1,$2,$3,$4,$5);
                """, scope.Connection, scope.Transaction);
            insert.Parameters.AddWithValue(record.ClarificationId); insert.Parameters.AddWithValue(caseId.Value);
            insert.Parameters.AddWithValue(current.Version); insert.Parameters.AddWithValue(NpgsqlDbType.Json, json);
            insert.Parameters.AddWithValue(Hash(json)); await insert.ExecuteNonQueryAsync(token);
            token.ThrowIfCancellationRequested(); return record;
        }
        catch (PostgresException exception) when (exception.SqlState == "23505") { throw new CaseCorrectionConflictException(); }
        catch (NpgsqlException) { throw new CaseReviewStorageException(); }
    }

    public async Task ResolveClarificationAsync(string clarificationId, CaseCorrectionPlan correction, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(correction);
        var scope = ClarificationScope(correction.Link.CaseId, "CORRECT");
        try
        {
            await Lock(scope.Connection, scope.Transaction, correction.Link.CaseId, token);
            var request = await ReadClarification(scope.Connection, scope.Transaction, correction.Link.CaseId, clarificationId, token)
                ?? throw new CaseCorrectionBindingException();
            CaseClarificationService.VerifyResolution(request, correction);
            // Verify against the actual retained correction, rather than trusting the callback object.
            long version;
            await using (var query = new NpgsqlCommand(
                "SELECT review_version FROM normacase.case_correction_links WHERE correction_id=$1 AND case_id=$2",
                scope.Connection, scope.Transaction))
            {
                query.Parameters.AddWithValue(correction.Link.CorrectionId); query.Parameters.AddWithValue(correction.Link.CaseId.Value);
                version = (long?)await query.ExecuteScalarAsync(token) ?? throw new CaseCorrectionBindingException();
            }
            var retained = await ReadCorrection(scope.Connection, scope.Transaction, correction.Link.CaseId, version, token)
                ?? throw new CaseCorrectionBindingException();
            if (CaseCorrectionLinkJson.Serialize(retained) != CaseCorrectionLinkJson.Serialize(correction.Link)
                || retained.ActorId != scope.State.ActorId) throw new CaseCorrectionBindingException();
            var input = await PostgresNormalizedIntakeStore.Read(scope.Connection, scope.Transaction,
                retained.SourceSystemId, retained.UpstreamCaseId, retained.CaseRevision, token)
                ?? throw new CaseReviewIntegrityException();
            if (!input.HasSameContent(correction.Input)) throw new CaseCorrectionBindingException();
            await using var insert = new NpgsqlCommand(
                "INSERT INTO normacase.case_clarification_resolutions(clarification_id,correction_id) VALUES($1,$2)",
                scope.Connection, scope.Transaction);
            insert.Parameters.AddWithValue(clarificationId); insert.Parameters.AddWithValue(retained.CorrectionId);
            await insert.ExecuteNonQueryAsync(token); token.ThrowIfCancellationRequested();
        }
        catch (PostgresException exception) when (exception.SqlState == "23505") { throw new CaseCorrectionConflictException(); }
        catch (NpgsqlException) { throw new CaseReviewStorageException(); }
    }

    public async Task<IReadOnlyList<CaseClarificationEntry>> ListClarificationsAsync(CaseId caseId,
        int pageSize, string? afterId, CancellationToken token = default)
    {
        if (caseId.IsEmpty || pageSize is < 1 or > 100 || afterId is "" || afterId?.Length > 128)
            throw new ArgumentException("Invalid clarification page.");
        try
        {
            await using var session = await PostgresOperationSession.OpenAsync(source, token);
            await Lock(session.Connection, session.Transaction, caseId, token);
            await Read(session.Connection, session.Transaction, caseId, token);
            var ids = new List<(string Id, string? Correction)>();
            await using (var query = new NpgsqlCommand("""
                SELECT q.clarification_id,r.correction_id FROM normacase.case_clarification_requests q
                LEFT JOIN normacase.case_clarification_resolutions r USING(clarification_id)
                WHERE q.case_id=$1 AND ($2::text IS NULL OR q.clarification_id COLLATE "C">$2 COLLATE "C")
                ORDER BY q.clarification_id COLLATE "C" LIMIT $3;
                """, session.Connection, session.Transaction))
            {
                query.Parameters.AddWithValue(caseId.Value);
                query.Parameters.Add(new NpgsqlParameter { NpgsqlDbType=NpgsqlDbType.Text, Value=(object?)afterId??DBNull.Value });
                query.Parameters.AddWithValue(pageSize+1);
                await using var reader = await query.ExecuteReaderAsync(token);
                while (await reader.ReadAsync(token)) ids.Add((reader.GetString(0), reader.IsDBNull(1)?null:reader.GetString(1)));
            }
            var results = new List<CaseClarificationEntry>();
            foreach (var id in ids)
            {
                var request = await ReadClarification(session.Connection, session.Transaction, caseId, id.Id, token)
                    ?? throw new CaseReviewIntegrityException();
                if (id.Correction is not null)
                {
                    long version;
                    await using (var query = new NpgsqlCommand(
                        "SELECT review_version FROM normacase.case_correction_links WHERE correction_id=$1 AND case_id=$2",
                        session.Connection, session.Transaction))
                    {
                        query.Parameters.AddWithValue(id.Correction); query.Parameters.AddWithValue(caseId.Value);
                        version=(long?)await query.ExecuteScalarAsync(token)??throw new CaseReviewIntegrityException();
                    }
                    var link=await ReadCorrection(session.Connection,session.Transaction,caseId,version,token)??throw new CaseReviewIntegrityException();
                    var input=await PostgresNormalizedIntakeStore.Read(session.Connection,session.Transaction,
                        link.SourceSystemId,link.UpstreamCaseId,link.CaseRevision,token)??throw new CaseReviewIntegrityException();
                    if (request.AssessmentId!=link.PreviousAssessmentId.Value || request.CaseRevision!=link.PreviousCaseRevision
                        || request.RecordedAtUtc>link.RecordedAtUtc || !TargetsPresent(request,input))
                        throw new CaseReviewIntegrityException();
                }
                results.Add(new(request,id.Correction));
            }
            return results.AsReadOnly();
        }
        catch (NpgsqlException) { throw new CaseReviewStorageException(); }
    }

    private PostgresAuthorizedOperation.Context ClarificationScope(CaseId caseId, string action)
    {
        var scope = PostgresAuthorizedOperation.Current ?? throw new CaseCorrectionDeniedException();
        if (!ReferenceEquals(scope.Source,source) || !scope.State.Actions.Contains("READ",StringComparer.Ordinal)
            || !scope.State.Actions.Contains(action,StringComparer.Ordinal) || !scope.State.CaseIds.Contains(caseId.Value,StringComparer.Ordinal))
            throw new CaseCorrectionDeniedException();
        return scope;
    }
    private async Task<CaseClarificationRecord?> ReadClarification(NpgsqlConnection connection,NpgsqlTransaction transaction,
        CaseId caseId,string id,CancellationToken token)
    {
        string json;long version;
        await using(var query=new NpgsqlCommand(
            "SELECT review_version,request_json::text,request_sha256 FROM normacase.case_clarification_requests WHERE case_id=$1 AND clarification_id=$2",
            connection,transaction))
        {
            query.Parameters.AddWithValue(caseId.Value);query.Parameters.AddWithValue(id);
            await using var reader=await query.ExecuteReaderAsync(token);if(!await reader.ReadAsync(token))return null;
            version=reader.GetInt64(0);json=reader.GetString(1);
            if(Hash(json)!=reader.GetString(2))throw new CaseReviewIntegrityException();
        }
        try
        {
            var record=CaseClarificationJson.Deserialize(json);
            if(record.ClarificationId!=id || record.CaseId!=caseId)throw new CaseReviewIntegrityException();
            await using var stateQuery=new NpgsqlCommand(
                "SELECT state_json::text FROM normacase.case_review_versions WHERE case_id=$1 AND version=$2",connection,transaction);
            stateQuery.Parameters.AddWithValue(caseId.Value);stateQuery.Parameters.AddWithValue(version);
            var stateJson=(string?)await stateQuery.ExecuteScalarAsync(token)??throw new CaseReviewIntegrityException();
            if(!Matches(record,CaseReviewStateJson.Deserialize(stateJson,resolver)))throw new CaseReviewIntegrityException();
            return record;
        }
        catch(Exception exception)when(exception is ArgumentException or JsonException){throw new CaseReviewIntegrityException();}
    }
    private static bool Matches(CaseClarificationRecord record,CaseReviewState state)
        =>record.CaseId==state.Process.CaseId&&record.AssessmentId==state.Assessment.AssessmentId.Value
            &&record.CaseRevision==state.Process.CaseRevision&&record.ProcessRevision==state.Process.Revision
            &&record.AuditRevision==state.Audit.Events[^1].Sequence&&record.RecordedAtUtc>=state.Audit.Events[^1].OccurredAt;
    private static bool TargetsPresent(CaseClarificationRecord request,NormaCase.Application.Intake.NormalizedIntakeRecord input)
        =>request.RequestedFields.All(x=>input.Input.Facts.TryGetValue(x,out var value)&&!value.IsUnknown)
            &&request.RequestedEvidence.All(x=>input.Input.Evidence.GetValueOrDefault(x,NormaCase.Domain.Evidence.EvidenceStatus.Missing)==NormaCase.Domain.Evidence.EvidenceStatus.Present);
}
