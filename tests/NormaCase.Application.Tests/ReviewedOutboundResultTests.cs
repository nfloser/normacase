using NormaCase.Application.Assessments;
using NormaCase.Application.Intake;
using NormaCase.Application.Outbound;
using NormaCase.Application.Reviews;
using NormaCase.Domain.Audit;
using NormaCase.Domain.Cases;
using NormaCase.Domain.Decision;
using NormaCase.Domain.Evidence;
using NormaCase.Domain.Workflow;
using NormaCase.Knowledge.Model;
using NormaCase.Knowledge.Serialization;
using Xunit;

namespace NormaCase.Application.Tests;

public sealed class ReviewedOutboundResultTests
{
    private static readonly DateTimeOffset AssessmentTime = new(2026,10,3,14,0,0,TimeSpan.Zero);
    private static readonly WorkflowDefinition Workflow = new("synthetic-outbound",3,"awaiting-review",
        [new("awaiting-review",false),new("accepted",true),new("overridden",true)],
        [new("accept","awaiting-review","accepted"),new("override","awaiting-review","overridden")]);

    [Theory]
    [InlineData(HumanReviewDisposition.AcceptSystemResult, "accepted")]
    [InlineData(HumanReviewDisposition.Override, "overridden")]
    public void Reviewed_terminal_case_builds_transport_neutral_result(HumanReviewDisposition disposition,string terminal)
    {
        var (intake,state)=Ready(disposition,terminal);
        var result=new ReviewedOutboundResultBuilder().Build(intake,state,Workflow,
            new("outbound-message-1","correlation-1",7,1,2));

        Assert.Equal("synthetic-source",result.Provenance.SourceSystemId);
        Assert.Equal("upstream-case-42",result.Provenance.UpstreamCaseId);
        Assert.Equal("upstream-message-9",result.Provenance.MessageId);
        Assert.Equal(11,result.Provenance.UpstreamRevision);
        Assert.Equal(state.Assessment.AssessmentId,result.AssessmentId);
        Assert.Equal(7,result.CaseRevision);
        Assert.Equal(1,result.ProcessRevision);
        Assert.Equal(2,result.AuditRevision);
        Assert.Equal("synthetic-outbound",result.WorkflowId);
        Assert.Equal(3,result.WorkflowVersion);
        Assert.Equal(terminal,result.StateId);
        Assert.Equal(disposition,result.Disposition);
        Assert.Equal("review-1",result.ReviewId.Value);
        Assert.Equal(state.Assessment.Result.Outcome,result.OriginalOutcome);
        Assert.Equal(disposition==HumanReviewDisposition.Override?AssessmentOutcome.NotSupported:state.Assessment.Result.Outcome,result.HumanOutcome);
        Assert.Equal(intake.Input.Facts.OrderBy(item=>item.Key),result.Input.Facts.OrderBy(item=>item.Key));
        Assert.Equal(intake.Input.Evidence.OrderBy(item=>item.Key),result.Input.Evidence.OrderBy(item=>item.Key));
        Assert.Equal("synthetic-document-1",result.EvidenceReferences["verification"].Single());
    }

    [Theory]
    [InlineData(8,1,2)]
    [InlineData(7,0,2)]
    [InlineData(7,1,1)]
    public void Stale_revisions_fail_closed(long caseRevision,long processRevision,long auditRevision)
    {
        var (intake,state)=Ready();
        Assert.Throws<OutboundResultConflictException>(()=>new ReviewedOutboundResultBuilder().Build(
            intake,state,Workflow,new("message","correlation",caseRevision,processRevision,auditRevision)));
    }

    [Fact]
    public void Nonterminal_or_missing_review_cannot_be_exported()
    {
        var (intake,state)=Ready();
        var initial=CaseProcessingInstance.Start(state.Assessment.CaseId,7,Workflow);
        var audit=AssessmentAuditTrail.Start(AssessmentAuditEvent.AssessmentCreated(1,state.Assessment.AssessmentId,
            state.Assessment.RecordedAtUtc,"synthetic-ingest"));
        var notReady=new CaseReviewState(state.Assessment,7,initial,audit);
        Assert.Throws<OutboundResultNotReadyException>(()=>new ReviewedOutboundResultBuilder().Build(
            intake,notReady,Workflow,new("message","correlation",7,0,1)));
    }

    [Fact]
    public void Substituted_intake_or_knowledge_binding_is_rejected()
    {
        var (intake,state)=Ready();
        var request=Request(TruthValue.No);
        var changed=new NormalizedIntakeService(new NeverStore()).Normalize(request,Pack());
        Assert.Throws<OutboundResultBindingException>(()=>new ReviewedOutboundResultBuilder().Build(
            changed,state,Workflow,new("message","correlation",7,1,2)));

    }

    [Fact]
    public void Assessment_revision_and_original_audit_timestamp_are_bound()
    {
        var (intake,state)=Ready();
        var wrongRevision=state with { AssessmentCaseRevision=6 };
        Assert.Throws<OutboundResultBindingException>(()=>new ReviewedOutboundResultBuilder().Build(
            intake,wrongRevision,Workflow,new("message","correlation",7,1,2)));

        var wrongAudit=AssessmentAuditTrail.Start(AssessmentAuditEvent.AssessmentCreated(1,state.Assessment.AssessmentId,
            state.Assessment.RecordedAtUtc.AddSeconds(1),"synthetic-ingest"))
            .Append(AssessmentAuditEvent.HumanReviewRecorded(2,state.Audit.Events[^1].Review!));
        var wrongTimestamp=state with { Audit=wrongAudit };
        Assert.Throws<OutboundResultBindingException>(()=>new ReviewedOutboundResultBuilder().Build(
            intake,wrongTimestamp,Workflow,new("message","correlation",7,1,2)));
    }

    [Fact]
    public void Returned_input_and_references_are_detached()
    {
        var (intake,state)=Ready();
        var result=new ReviewedOutboundResultBuilder().Build(intake,state,Workflow,new("message","correlation",7,1,2));
        Assert.Throws<NotSupportedException>(()=>((IDictionary<string,CaseValue>)result.Input.Facts).Clear());
        Assert.Throws<NotSupportedException>(()=>((IDictionary<string,IReadOnlyList<string>>)result.EvidenceReferences).Clear());
    }

    private static (NormalizedIntakeRecord Intake,CaseReviewState State) Ready(
        HumanReviewDisposition disposition=HumanReviewDisposition.AcceptSystemResult,string terminal="accepted")
    {
        var pack=Pack();
        var intake=new NormalizedIntakeService(new NeverStore()).Normalize(Request(),pack);
        var assessment=new AssessmentRecorder().Evaluate(pack,intake.Input.Facts,intake.Input.AssessmentDate,intake.Input.Evidence,
            new(new("assessment-outbound"),intake.CaseId,"platform-1.4.0",AssessmentTime));
        var process=CaseProcessingInstance.Start(intake.CaseId,7,Workflow).Apply(Workflow,0,
            disposition==HumanReviewDisposition.Override?"override":"accept");
        Assert.Equal(terminal,process.StateId);
        var audit=AssessmentAuditTrail.Start(AssessmentAuditEvent.AssessmentCreated(1,assessment.AssessmentId,AssessmentTime,"synthetic-ingest"))
            .Append(AssessmentAuditEvent.HumanReviewRecorded(2,new(new("review-1"),assessment.AssessmentId,
                "synthetic-local:reviewer",AssessmentTime.AddMinutes(2),disposition,"Synthetic reviewed result",
                disposition==HumanReviewDisposition.Override?AssessmentOutcome.NotSupported:null,new("ticket","synthetic-42"))));
        return (intake,new(assessment,7,process,audit));
    }

    private static NormalizedIntakeRequest Request(TruthValue confirmed=TruthValue.Yes)=>new(
        new("case-outbound"),"synthetic-type",
        new("synthetic-source","upstream-case-42","upstream-message-9",11,"synthetic-adapter",2,new(2026,10,3,13,55,0,TimeSpan.Zero)),
        new(2026,10,3),
        new Dictionary<string,CaseValue>{{"request_confirmed",confirmed},{"measurement",15m},{"alternative_confirmed",TruthValue.No}},
        new Dictionary<string,EvidenceStatus>{{"verification",EvidenceStatus.Present}},
        new Dictionary<string,IReadOnlyList<string>>{{"verification",new[]{"synthetic-document-1"}}});

    private static KnowledgePack Pack()=>new KnowledgePackLoader().LoadFromFile(
        Path.Combine(AppContext.BaseDirectory,"Fixtures/demo-c-pack.json"));

    private sealed class NeverStore:INormalizedIntakeStore
    {
        public Task<IntakeReceipt> AppendAsync(NormalizedIntakeRecord record,CancellationToken cancellationToken=default)
            => throw new InvalidOperationException("Normalization-only fixture.");
    }
}
