using NormaCase.Application.Assessments;
using NormaCase.Application.Processing;
using NormaCase.Application.Triage;
using NormaCase.Domain.Audit;
using NormaCase.Domain.Cases;
using NormaCase.Domain.Decision;
using NormaCase.Domain.Evidence;
using NormaCase.Domain.Workflow;
using NormaCase.Knowledge.Serialization;
using Xunit;

namespace NormaCase.Application.Tests;

public sealed class CaseProcessingRoutingServiceTests
{
    private static readonly ApprovalRoutingPolicy AssessmentPolicy = new(
        "synthetic-assessment-routing",
        2,
        [AssessmentOutcome.Supported, AssessmentOutcome.NotSupported]);

    [Fact]
    public void Ready_for_approval_routes_to_an_explicit_waiting_state_not_approval()
    {
        var routing = Route(TruthValue.Yes);
        var definition = Process();
        var process = CaseProcessingInstance.Start(routing.CaseId, 4, definition);

        var result = new CaseProcessingRoutingService().Apply(
            routing,
            process,
            definition,
            Policy(definition),
            expectedCaseRevision: 4,
            expectedProcessRevision: 0);

        Assert.Equal(CaseProcessingRoutingStatus.Applied, result.Status);
        Assert.Equal(CaseProcessingRoutingReason.None, result.Reason);
        Assert.Equal("prepare-approval", result.TransitionId);
        Assert.Equal("awaiting-approval", result.Process.StateId);
        Assert.Equal(1, result.Process.Revision);
        Assert.Equal("received", process.StateId);
        Assert.Equal(0, process.Revision);
        Assert.Equal(routing.AssessmentId, result.AssessmentId);
        Assert.Equal(AssessmentRoutingDisposition.ReadyForApproval, result.RoutingDisposition);
    }

    [Fact]
    public void Incomplete_and_human_review_take_distinct_explicit_transitions()
    {
        var definition = Process();
        var service = new CaseProcessingRoutingService();

        var incomplete = Route(TruthValue.Unknown);
        var clarification = service.Apply(
            incomplete,
            CaseProcessingInstance.Start(incomplete.CaseId, 8, definition),
            definition,
            Policy(definition),
            8,
            0);

        var reviewRouting = Route(TruthValue.Yes, EvidenceStatus.Missing);
        var review = service.Apply(
            reviewRouting,
            CaseProcessingInstance.Start(reviewRouting.CaseId, 9, definition),
            definition,
            Policy(definition),
            9,
            0);

        Assert.Equal(AssessmentRoutingDisposition.Incomplete, incomplete.Disposition);
        Assert.Equal("request-information", clarification.TransitionId);
        Assert.Equal("waiting-information", clarification.Process.StateId);
        Assert.Equal(AssessmentRoutingDisposition.HumanReview, reviewRouting.Disposition);
        Assert.Equal("request-review", review.TransitionId);
        Assert.Equal("manual-review", review.Process.StateId);
    }

    [Fact]
    public void Unmapped_disposition_fails_closed_without_changing_process()
    {
        var routing = Route(TruthValue.Yes, EvidenceStatus.Missing);
        var definition = Process();
        var process = CaseProcessingInstance.Start(routing.CaseId, 3, definition);
        var policy = new CaseProcessingRoutingPolicy(
            "partial",
            1,
            AssessmentPolicy.Id,
            AssessmentPolicy.Version,
            definition.Id,
            definition.Version,
            new Dictionary<AssessmentRoutingDisposition, string>
            {
                [AssessmentRoutingDisposition.ReadyForApproval] = "prepare-approval"
            });

        var result = new CaseProcessingRoutingService().Apply(
            routing, process, definition, policy, 3, 0);

        Assert.Equal(CaseProcessingRoutingStatus.Unresolved, result.Status);
        Assert.Equal(CaseProcessingRoutingReason.UnmappedDisposition, result.Reason);
        Assert.Null(result.TransitionId);
        Assert.Same(process, result.Process);
        Assert.Equal("received", process.StateId);
        Assert.Equal(0, process.Revision);
    }

    [Fact]
    public void Mismatched_case_assessment_policy_or_workflow_fails_closed()
    {
        var routing = Route(TruthValue.Yes);
        var definition = Process();
        var service = new CaseProcessingRoutingService();

        var wrongCase = service.Apply(
            routing,
            CaseProcessingInstance.Start(new("other-case"), 1, definition),
            definition,
            Policy(definition),
            1,
            0);
        Assert.Equal(CaseProcessingRoutingReason.CaseMismatch, wrongCase.Reason);

        var wrongAssessmentPolicy = service.Apply(
            routing,
            CaseProcessingInstance.Start(routing.CaseId, 1, definition),
            definition,
            new CaseProcessingRoutingPolicy(
                "process-routing",
                1,
                "other-assessment-policy",
                1,
                definition.Id,
                definition.Version,
                AllTransitions()),
            1,
            0);
        Assert.Equal(
            CaseProcessingRoutingReason.AssessmentPolicyMismatch,
            wrongAssessmentPolicy.Reason);

        var wrongWorkflow = service.Apply(
            routing,
            CaseProcessingInstance.Start(routing.CaseId, 1, definition),
            definition,
            new CaseProcessingRoutingPolicy(
                "process-routing",
                1,
                AssessmentPolicy.Id,
                AssessmentPolicy.Version,
                "another-process",
                1,
                AllTransitions()),
            1,
            0);
        Assert.Equal(CaseProcessingRoutingReason.WorkflowMismatch, wrongWorkflow.Reason);
    }

    [Fact]
    public void Transition_must_be_available_from_the_current_state()
    {
        var routing = Route(TruthValue.Yes);
        var definition = Process();
        var initial = CaseProcessingInstance.Start(routing.CaseId, 2, definition);
        var applied = new CaseProcessingRoutingService().Apply(
            routing, initial, definition, Policy(definition), 2, 0);

        var second = new CaseProcessingRoutingService().Apply(
            routing,
            applied.Process,
            definition,
            Policy(definition),
            2,
            1);

        Assert.Equal(CaseProcessingRoutingStatus.Unresolved, second.Status);
        Assert.Equal(
            CaseProcessingRoutingReason.TransitionUnavailable,
            second.Reason);
        Assert.Equal("awaiting-approval", second.Process.StateId);
        Assert.Equal(1, second.Process.Revision);
    }

    [Fact]
    public void Stale_case_or_process_revision_is_rejected_before_routing()
    {
        var routing = Route(TruthValue.Yes);
        var definition = Process();
        var process = CaseProcessingInstance.Start(routing.CaseId, 5, definition);
        var service = new CaseProcessingRoutingService();

        Assert.Throws<CaseProcessingRoutingConcurrencyException>(
            () => service.Apply(
                routing, process, definition, Policy(definition), 4, 0));

        Assert.Throws<CaseProcessingRoutingConcurrencyException>(
            () => service.Apply(
                routing, process, definition, Policy(definition), 5, 1));

        Assert.Throws<ArgumentOutOfRangeException>(
            () => service.Apply(
                routing, process, definition, Policy(definition), 0, 0));

        Assert.Throws<ArgumentOutOfRangeException>(
            () => service.Apply(
                routing, process, definition, Policy(definition), 5, -1));
    }

    [Fact]
    public void Routing_is_repeatable_and_does_not_mutate_the_routing_or_process()
    {
        var routing = Route(TruthValue.No);
        var definition = Process();
        var process = CaseProcessingInstance.Start(routing.CaseId, 6, definition);
        var service = new CaseProcessingRoutingService();
        var policy = Policy(definition);

        var first = service.Apply(routing, process, definition, policy, 6, 0);
        var second = service.Apply(routing, process, definition, policy, 6, 0);

        Assert.Equal(first.Status, second.Status);
        Assert.Equal(first.Reason, second.Reason);
        Assert.Equal(first.TransitionId, second.TransitionId);
        Assert.Equal(first.Process.StateId, second.Process.StateId);
        Assert.Equal(first.Process.Revision, second.Process.Revision);
        Assert.Equal(AssessmentRoutingDisposition.ReadyForApproval, routing.Disposition);
        Assert.Equal("received", process.StateId);
        Assert.Equal(0, process.Revision);
    }

    [Fact]
    public void Routing_policy_is_versioned_detached_and_validated()
    {
        var definition = Process();
        var mappings = AllTransitions();
        var policy = new CaseProcessingRoutingPolicy(
            "process-routing",
            3,
            AssessmentPolicy.Id,
            AssessmentPolicy.Version,
            definition.Id,
            definition.Version,
            mappings);
        mappings.Clear();

        Assert.True(policy.TryGetTransition(
            AssessmentRoutingDisposition.ReadyForApproval,
            out var transition));
        Assert.Equal("prepare-approval", transition);
        Assert.Equal(3, policy.Version);

        Assert.Throws<ArgumentException>(
            () => new CaseProcessingRoutingPolicy(
                "",
                1,
                AssessmentPolicy.Id,
                AssessmentPolicy.Version,
                definition.Id,
                definition.Version,
                AllTransitions()));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new CaseProcessingRoutingPolicy(
                "policy",
                0,
                AssessmentPolicy.Id,
                AssessmentPolicy.Version,
                definition.Id,
                definition.Version,
                AllTransitions()));
        Assert.Throws<ArgumentException>(
            () => new CaseProcessingRoutingPolicy(
                "policy",
                1,
                AssessmentPolicy.Id,
                AssessmentPolicy.Version,
                definition.Id,
                definition.Version,
                new Dictionary<AssessmentRoutingDisposition, string>
                {
                    [(AssessmentRoutingDisposition)99] = "transition"
                }));
    }

    private static AssessmentRouting Route(
        TruthValue value,
        EvidenceStatus evidence = EvidenceStatus.Present)
    {
        var pack = new KnowledgePackLoader().LoadFromFile(
            Path.Combine(AppContext.BaseDirectory, "Fixtures/demo-c-pack.json"));
        var record = new AssessmentRecorder().Evaluate(
            pack,
            new Dictionary<string, CaseValue>
            {
                ["request_confirmed"] = value,
                ["measurement"] = 15m,
                ["alternative_confirmed"] = TruthValue.No
            },
            new DateOnly(2026, 10, 3),
            new Dictionary<string, EvidenceStatus>
            {
                ["verification"] = evidence
            },
            new(
                new("assessment-routing-118"),
                new("case-routing-118"),
                "test-platform",
                new DateTimeOffset(
                    2026, 10, 3, 12, 0, 0, TimeSpan.Zero)));

        return new AssessmentTriageService().Route(record, AssessmentPolicy);
    }

    private static WorkflowDefinition Process()
        => new(
            "synthetic-case-process",
            3,
            "received",
            [
                new("received", false),
                new("awaiting-approval", false),
                new("waiting-information", false),
                new("manual-review", false)
            ],
            [
                new("prepare-approval", "received", "awaiting-approval"),
                new("request-information", "received", "waiting-information"),
                new("request-review", "received", "manual-review")
            ]);

    private static CaseProcessingRoutingPolicy Policy(
        WorkflowDefinition definition)
        => new(
            "synthetic-process-routing",
            4,
            AssessmentPolicy.Id,
            AssessmentPolicy.Version,
            definition.Id,
            definition.Version,
            AllTransitions());

    private static Dictionary<AssessmentRoutingDisposition, string> AllTransitions()
        => new()
        {
            [AssessmentRoutingDisposition.ReadyForApproval] = "prepare-approval",
            [AssessmentRoutingDisposition.Incomplete] = "request-information",
            [AssessmentRoutingDisposition.HumanReview] = "request-review"
        };
}
