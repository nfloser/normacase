using Npgsql;
using NormaCase.Application.Assessments;
using NormaCase.Application.Reviews;
using NormaCase.Domain.Audit;
using NormaCase.Domain.Cases;
using NormaCase.Persistence.PostgreSql;

namespace NormaCase.Api;

// Replaceable only by trusted host/test composition, never by HTTP input.
internal interface IReviewDemoRepository : ICaseReviewTransactionStore
{
    Task<CaseReviewState> GetAsync(SyntheticCaseSeed seed, SyntheticWorkload workload, CancellationToken token);
}
internal sealed class ReviewDemoRepository(NpgsqlDataSource source) : IReviewDemoRepository
{
    private readonly SemaphoreSlim initialization = new(1, 1);
    private PostgresCaseReviewStore? store;
    private bool migrated;
    public async Task<CaseReviewState> GetAsync(SyntheticCaseSeed seed, SyntheticWorkload workload, CancellationToken token)
    {
        await initialization.WaitAsync(token);
        try
        {
            store ??= new PostgresCaseReviewStore(source, (id, version) => id == workload.Workflow.Id && version == workload.Workflow.Version
                ? workload.Workflow : throw new ArgumentException("Unknown synthetic review workflow."));
            if (!migrated) { await new PostgresMigrationRunner(source).MigrateAsync(token); migrated = true; }
            var existing = await store.LoadAsync(seed.Process.CaseId, token);
            if (existing is not null) return existing;
            var records = new PostgresAssessmentRecordStore(source);
            var assessment = await records.LoadAsync(seed.Assessment!.AssessmentId, token);
            if (assessment is null)
            {
                assessment = seed.Assessment;
                await records.AppendAsync(assessment!, token);
            }
            var expected = seed.Assessment!;
            var comparable = new AssessmentRecord(expected.AssessmentId, expected.CaseId, expected.KnowledgePackId,
                assessment!.PlatformVersion, assessment.RecordedAtUtc, expected.Input, expected.Result);
            if (NormaCase.Serialization.AssessmentRecordJson.Serialize(comparable) != NormaCase.Serialization.AssessmentRecordJson.Serialize(assessment))
                throw new CaseReviewIntegrityException();
            var initial = new CaseReviewState(assessment!, seed.Process.CaseRevision, seed.Process,
                AssessmentAuditTrail.Start(AssessmentAuditEvent.AssessmentCreated(1, assessment!.AssessmentId, assessment.RecordedAtUtc, "synthetic-local:intake")));
            await store.InitializeAsync(initial, token);
            return initial;
        }
        finally { initialization.Release(); }
    }
    public Task<CaseReviewState> ExecuteAsync(CaseId caseId, Func<CaseReviewState, CaseReviewState> update, CancellationToken cancellationToken = default)
        => (store ?? throw new InvalidOperationException("Synthetic workload not initialized.")).ExecuteAsync(caseId, update, cancellationToken);
}
