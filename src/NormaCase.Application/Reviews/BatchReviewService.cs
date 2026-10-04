using NormaCase.Domain.Audit;
using NormaCase.Domain.Cases;
using NormaCase.Domain.Workflow;

namespace NormaCase.Application.Reviews;

public enum BatchReviewFailureMode
{
    Continue,
    Stop
}

public enum BatchReviewItemStatus
{
    Committed,
    Denied,
    Conflict,
    PolicyRejected,
    NotAttempted
}

public sealed class BatchReviewPolicy
{
    public const int AbsoluteMaximumCommands = 100;

    public BatchReviewPolicy(string id, int version, string reviewPolicyId,
        int reviewPolicyVersion, int maximumCommands, BatchReviewFailureMode failureMode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(reviewPolicyId);
        if (version < 1) throw new ArgumentOutOfRangeException(nameof(version));
        if (reviewPolicyVersion < 1) throw new ArgumentOutOfRangeException(nameof(reviewPolicyVersion));
        if (maximumCommands is < 1 or > AbsoluteMaximumCommands)
            throw new ArgumentOutOfRangeException(nameof(maximumCommands));
        if (!Enum.IsDefined(failureMode)) throw new ArgumentOutOfRangeException(nameof(failureMode));
        Id = id;
        Version = version;
        ReviewPolicyId = reviewPolicyId;
        ReviewPolicyVersion = reviewPolicyVersion;
        MaximumCommands = maximumCommands;
        FailureMode = failureMode;
    }

    public string Id { get; }
    public int Version { get; }
    public string ReviewPolicyId { get; }
    public int ReviewPolicyVersion { get; }
    public int MaximumCommands { get; }
    public BatchReviewFailureMode FailureMode { get; }

    internal bool Matches(CaseReviewPolicy policy)
        => ReviewPolicyId == policy.Id && ReviewPolicyVersion == policy.Version;
}

public sealed record BatchReviewItemResult(
    CaseId CaseId,
    ReviewId ReviewId,
    BatchReviewItemStatus Status);

public sealed record BatchReviewResult(
    string PolicyId,
    int PolicyVersion,
    IReadOnlyList<BatchReviewItemResult> Items);

/// <summary>
/// Bounded orchestration over the existing single-case transaction. A batch is not
/// atomic as a whole: every committed item remains committed if a later item fails.
/// </summary>
public sealed class BatchReviewService
{
    private readonly CaseReviewService reviews;

    public BatchReviewService(CaseReviewService reviews)
    {
        ArgumentNullException.ThrowIfNull(reviews);
        this.reviews = reviews;
    }

    public async Task<BatchReviewResult> ReviewAsync(
        AuthenticatedReviewActor actor,
        IEnumerable<CaseReviewCommand> commands,
        WorkflowDefinition workflow,
        CaseReviewPolicy reviewPolicy,
        BatchReviewPolicy batchPolicy,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(commands);
        ArgumentNullException.ThrowIfNull(workflow);
        ArgumentNullException.ThrowIfNull(reviewPolicy);
        ArgumentNullException.ThrowIfNull(batchPolicy);
        if (!batchPolicy.Matches(reviewPolicy))
            throw new ArgumentException("Batch policy does not match review policy.", nameof(batchPolicy));

        var items = commands.ToArray();
        if (items.Length is 0 || items.Length > batchPolicy.MaximumCommands)
            throw new ArgumentOutOfRangeException(nameof(commands));
        if (items.Any(item => item is null)) throw new ArgumentException("Null batch command.", nameof(commands));
        if (items.Select(item => item.CaseId).Distinct().Count() != items.Length
            || items.Select(item => item.ReviewId).Distinct().Count() != items.Length)
            throw new ArgumentException("Batch case and review identities must be unique.", nameof(commands));

        // Validate the complete request before the first independently committed case.
        foreach (var command in items) CaseReviewService.ValidateCommand(actor, command);

        var results = new List<BatchReviewItemResult>(items.Length);
        var stopped = false;
        foreach (var command in items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (stopped)
            {
                results.Add(new(command.CaseId, command.ReviewId, BatchReviewItemStatus.NotAttempted));
                continue;
            }

            var status = await ReviewOne(actor, command, workflow, reviewPolicy, cancellationToken);
            results.Add(new(command.CaseId, command.ReviewId, status));
            stopped = status != BatchReviewItemStatus.Committed
                && batchPolicy.FailureMode == BatchReviewFailureMode.Stop;
        }

        return new(batchPolicy.Id, batchPolicy.Version, results.ToArray());
    }

    private async Task<BatchReviewItemStatus> ReviewOne(
        AuthenticatedReviewActor actor,
        CaseReviewCommand command,
        WorkflowDefinition workflow,
        CaseReviewPolicy policy,
        CancellationToken cancellationToken)
    {
        try
        {
            await reviews.ReviewAsync(actor, command, workflow, policy, cancellationToken);
            return BatchReviewItemStatus.Committed;
        }
        catch (CaseReviewDeniedException)
        {
            return BatchReviewItemStatus.Denied;
        }
        catch (CaseReviewConflictException)
        {
            return BatchReviewItemStatus.Conflict;
        }
        catch (CaseReviewPolicyException)
        {
            return BatchReviewItemStatus.PolicyRejected;
        }
    }
}
