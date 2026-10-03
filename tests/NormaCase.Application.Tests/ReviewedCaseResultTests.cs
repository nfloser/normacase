using NormaCase.Application.Assessments;
using NormaCase.Application.Intake;
using NormaCase.Application.Outbound;
using NormaCase.Application.Reviews;
using NormaCase.Domain.Audit;
using NormaCase.Domain.Cases;
using NormaCase.Domain.Decision;
using NormaCase.Domain.Workflow;
using NormaCase.Knowledge.Serialization;
using Xunit;

namespace NormaCase.Application.Tests;
public sealed class ReviewedCaseResultTests
{
    private static readonly DateTimeOffset Time = new(2026,10,3,12,0,0,TimeSpan.Zero);
    private static readonly WorkflowDefinition Workflow = new("synthetic-outbound",1,"pending",
        [new("pending",false),new("done",true)],[new("review","pending","done")]);
    [Theory]
    [InlineData(HumanReviewDisposition.AcceptSystemResult,AssessmentOutcome.Supported)]
    [InlineData(HumanReviewDisposition.Override,AssessmentOutcome.NotSupported)]
    public void Result_retains_upstream_correlation_versions_original_and_human_outcomes(HumanReviewDisposition disposition,AssessmentOutcome expected)
    {
        var (intake,state)=Fixture(disposition);
        var result=new ReviewedCaseResultFactory().Create(intake,state,Workflow,new(1,1,2),"outbound-message","synthetic-correlation");
        Assert.Equal("upstream-case",result.UpstreamCaseId);
        Assert.Equal("upstream-message",result.UpstreamMessageId);
        Assert.Equal(7,result.UpstreamRevision);
        Assert.Equal("synthetic-correlation",result.CorrelationId);
        Assert.Equal(state.Assessment.Result.KnowledgeRelease,result.KnowledgeRelease);
        Assert.Equal(AssessmentOutcome.Supported,result.OriginalOutcome);
        Assert.Equal(expected,result.HumanOutcome);
        Assert.Equal(disposition,result.ReviewDisposition);
        Assert.Equal("synthetic-review",result.ReviewId);
        Assert.Equal(2,result.AuditRevision);
    }
    [Fact]
    public void Stale_revision_nonterminal_and_unreviewed_states_cannot_be_exported()
    {
        var (intake,state)=Fixture();var factory=new ReviewedCaseResultFactory();
        Assert.Throws<OutboundResultConflictException>(()=>factory.Create(intake,state,Workflow,new(1,0,2),"message","correlation"));
        Assert.Throws<OutboundResultConflictException>(()=>factory.Create(intake,state,Workflow,new(2,1,2),"message","correlation"));
        Assert.Throws<OutboundResultConflictException>(()=>factory.Create(intake,state,Workflow,new(1,1,1),"message","correlation"));
        var pending=state with {Process=CaseProcessingInstance.Start(state.Process.CaseId,1,Workflow)};
        Assert.Throws<OutboundResultBindingException>(()=>factory.Create(intake,pending,Workflow,new(1,0,2),"message","correlation"));
        var unreviewed=state with {Audit=AssessmentAuditTrail.Start(state.Audit.Events[0])};
        Assert.Throws<OutboundResultBindingException>(()=>factory.Create(intake,unreviewed,Workflow,new(1,1,1),"message","correlation"));
    }
    [Fact]
    public void Substituted_intake_values_case_or_workflow_are_rejected()
    {
        var (intake,state)=Fixture();var factory=new ReviewedCaseResultFactory();
        var (changed,_)=Fixture(value:TruthValue.No);
        Assert.Throws<OutboundResultBindingException>(()=>factory.Create(changed,state,Workflow,new(1,1,2),"message","correlation"));
        Assert.Throws<OutboundResultBindingException>(()=>factory.Create(intake,state with {AssessmentCaseRevision=2},Workflow,new(1,1,2),"message","correlation"));
        var other=state with {Process=CaseProcessingInstance.Start(new("other-case"),1,Workflow).Apply(Workflow,0,"review")};
        Assert.Throws<OutboundResultBindingException>(()=>factory.Create(intake,other,Workflow,new(1,1,2),"message","correlation"));
        var wrong=new WorkflowDefinition("wrong",1,"done",[new("done",true)],[]);
        Assert.Throws<OutboundResultBindingException>(()=>factory.Create(intake,state,wrong,new(1,1,2),"message","correlation"));
    }
    private static (NormalizedIntakeRecord,CaseReviewState) Fixture(HumanReviewDisposition disposition=HumanReviewDisposition.AcceptSystemResult,TruthValue value=TruthValue.Yes)
    {
        var pack=new KnowledgePackLoader().LoadFromFile(Path.Combine(AppContext.BaseDirectory,"Fixtures/demo-a-pack.json"));
        var request=new NormalizedIntakeRequest(new("case-outbound"),"synthetic-type",
            new("synthetic-source","upstream-case","upstream-message",7,"synthetic-adapter",1,Time),
            new(2026,10,3),new Dictionary<string,CaseValue>{{"criterion_a",value},{"criterion_b",TruthValue.Yes}},new Dictionary<string,NormaCase.Domain.Evidence.EvidenceStatus>(),new Dictionary<string,IReadOnlyList<string>>());
        var intake=new NormalizedIntakeService(new UnusedStore()).Normalize(request,pack);
        var assessment=new AssessmentRecorder().Evaluate(pack,intake.Input.Facts,intake.Input.AssessmentDate,intake.Input.Evidence,new(new("assessment-outbound"),intake.CaseId,"synthetic-platform",Time));
        var review=new HumanReviewRecord(new("synthetic-review"),assessment.AssessmentId,"synthetic-local:reviewer",Time.AddMinutes(1),disposition,"Synthetic review",disposition==HumanReviewDisposition.Override?AssessmentOutcome.NotSupported:null);
        var audit=AssessmentAuditTrail.Start(AssessmentAuditEvent.AssessmentCreated(1,assessment.AssessmentId,Time,"synthetic-ingest")).Append(AssessmentAuditEvent.HumanReviewRecorded(2,review));
        return (intake,new(assessment,1,CaseProcessingInstance.Start(intake.CaseId,1,Workflow).Apply(Workflow,0,"review"),audit));
    }
    private sealed class UnusedStore:INormalizedIntakeStore
    {
        public Task<IntakeReceipt> AppendAsync(NormalizedIntakeRecord record,CancellationToken token=default)=>throw new NotSupportedException();
    }
}
