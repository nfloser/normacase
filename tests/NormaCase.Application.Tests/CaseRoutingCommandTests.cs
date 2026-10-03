using NormaCase.Application.Assessments;
using NormaCase.Application.Intake;
using NormaCase.Application.Triage;
using NormaCase.Domain.Cases;
using NormaCase.Domain.Decision;
using NormaCase.Domain.Evidence;
using NormaCase.Domain.Workflow;
using NormaCase.Knowledge.Serialization;
using Xunit;

namespace NormaCase.Application.Tests;

public sealed class CaseRoutingCommandTests
{
    [Theory]
    [InlineData(TruthValue.Yes, EvidenceStatus.Present, "prepare")]
    [InlineData(TruthValue.No, EvidenceStatus.Present, "prepare")]
    [InlineData(TruthValue.Unknown, EvidenceStatus.Present, "request-info")]
    [InlineData(TruthValue.Yes, EvidenceStatus.Missing, "review")]
    [InlineData(TruthValue.Yes, EvidenceStatus.Conflicting, "review")]
    public void Normalized_recorded_cases_produce_explicit_reproducible_commands(TruthValue truth, EvidenceStatus evidence, string expected)
    {
        var (intake, assessment) = Recorded(truth, evidence);
        var definition = Definition();
        var process = CaseProcessingInstance.Start(intake.CaseId, intake.Provenance.UpstreamRevision, definition);
        var policy = Policy(definition);
        var service = new CaseRoutingCommandService();
        var plan = service.Plan(intake, assessment, process, policy);
        Assert.Equal(expected, plan.TransitionId);
        Assert.Equal("received", process.StateId);
        Assert.Equal(0, plan.ExpectedRevision);
        Assert.Equal(intake.CaseId, plan.CaseId);
        Assert.Equal(assessment.AssessmentId, plan.AssessmentId);
        Assert.Equal(1, plan.PolicyVersion);
        Assert.Equal(plan.TransitionId, service.Plan(intake, assessment, process, policy).TransitionId);
        var applied = process.Apply(definition, plan.ExpectedRevision, plan.TransitionId!);
        Assert.Equal(1, applied.Revision);
        Assert.Throws<CaseProcessingConcurrencyException>(() => applied.Apply(definition, plan.ExpectedRevision, plan.TransitionId!));
    }

    [Fact]
    public void Missing_mapping_returns_unresolved_without_transition_or_implicit_default()
    {
        var (intake, assessment) = Recorded();
        var definition = Definition();
        var plan = new CaseRoutingCommandService().Plan(intake, assessment,
            CaseProcessingInstance.Start(intake.CaseId, 1, definition),
            new("empty-policy",1,definition,new("allow-known",1,[AssessmentOutcome.Supported]),[],[]));
        Assert.Null(plan.TransitionId);
        Assert.Equal(RoutingPlanStatus.Unresolved, plan.Status);
    }

    [Fact]
    public void Technical_failure_uses_explicit_configured_transition_and_never_an_approval_route()
    {
        var (intake, _) = Recorded();
        var definition = Definition();
        var process = CaseProcessingInstance.Start(intake.CaseId, 1, definition);
        var plan = new CaseRoutingCommandService().PlanTechnicalFailure(intake, process, Policy(definition));
        Assert.Equal("technical",plan.TransitionId);
        Assert.Null(plan.AssessmentId);
        Assert.Equal("error",process.Apply(definition,0,plan.TransitionId!).StateId);
    }

    [Fact]
    public void Identity_case_revision_knowledge_and_input_substitution_fail_closed()
    {
        var (intake, assessment) = Recorded();
        var definition = Definition();
        var service = new CaseRoutingCommandService();
        var process = CaseProcessingInstance.Start(intake.CaseId, 1, definition);
        Assert.Throws<ArgumentException>(() => service.Plan(intake, assessment,CaseProcessingInstance.Start(new("other"),1,definition),Policy(definition)));
        Assert.Throws<ArgumentException>(() => service.Plan(intake, assessment,CaseProcessingInstance.Start(intake.CaseId,2,definition),Policy(definition)));
        var changed = new AssessmentRecord(assessment.AssessmentId, assessment.CaseId,"another-pack",assessment.PlatformVersion,assessment.RecordedAtUtc,assessment.Input,assessment.Result);
        Assert.Throws<ArgumentException>(() => service.Plan(intake,changed,process,Policy(definition)));
        var input = new AssessmentInputSnapshot(assessment.Input.AssessmentDate,new Dictionary<string,CaseValue>{["request_confirmed"]=TruthValue.No},assessment.Input.Evidence);
        changed = new(assessment.AssessmentId,assessment.CaseId,assessment.KnowledgePackId,assessment.PlatformVersion,assessment.RecordedAtUtc,input,assessment.Result);
        Assert.Throws<ArgumentException>(() => service.Plan(intake,changed,process,Policy(definition)));
    }

    [Fact]
    public void Policy_rejects_ambiguous_mappings_wrong_graph_and_technical_overlap()
    {
        var definition=Definition();
        var known = new ApprovalRoutingPolicy("known",1,[AssessmentOutcome.Supported]);
        Assert.Throws<ArgumentException>(()=>new CaseRoutingPolicy("test",1,definition,known,[new("received",AssessmentRoutingDisposition.ReadyForApproval,"unknown")],[]));
        Assert.Throws<ArgumentException>(()=>new CaseRoutingPolicy("test",1,definition,known,[new("received",AssessmentRoutingDisposition.ReadyForApproval,"prepare"),new("received",AssessmentRoutingDisposition.ReadyForApproval,"review")],[]));
        Assert.Throws<ArgumentException>(()=>new CaseRoutingPolicy("test",1,definition,known,[new("received",AssessmentRoutingDisposition.ReadyForApproval,"prepare")],[new("received","prepare")]));
    }

    [Fact]
    public void Second_materially_different_graph_uses_opaque_mapping_and_rejects_wrong_version()
    {
        var (intake,assessment)=Recorded();
        var other=new WorkflowDefinition("other-process",4,"x",[new("x",false),new("y",true)],[new("dispatch","x","y")]);
        var policy=new CaseRoutingPolicy("other-policy",2,other,new("other-outcomes",1,[AssessmentOutcome.Supported]),[new("x",AssessmentRoutingDisposition.ReadyForApproval,"dispatch")],[]);
        var plan=new CaseRoutingCommandService().Plan(intake,assessment,CaseProcessingInstance.Start(intake.CaseId,1,other),policy);
        Assert.Equal("dispatch",plan.TransitionId);
        Assert.Equal("other-policy",plan.PolicyId);
        Assert.Throws<ArgumentException>(()=>new CaseRoutingCommandService().Plan(intake,assessment,CaseProcessingInstance.Start(intake.CaseId,1,Definition()),policy));
    }

    private static WorkflowDefinition Definition()=>new("synthetic-case-process",1,"received",
        [new("received",false),new("prepared",true),new("information",true),new("review",true),new("error",true)],
        [new("prepare","received","prepared"),new("request-info","received","information"),new("review","received","review"),new("technical","received","error")]);
    private static CaseRoutingPolicy Policy(WorkflowDefinition definition)=>new("synthetic-commands",1,definition,
        new("synthetic-outcomes",1,[AssessmentOutcome.Supported,AssessmentOutcome.NotSupported]),
        [new("received",AssessmentRoutingDisposition.ReadyForApproval,"prepare"),new("received",AssessmentRoutingDisposition.Incomplete,"request-info"),new("received",AssessmentRoutingDisposition.HumanReview,"review")],
        [new("received","technical")]);
    private static (NormalizedIntakeRecord,AssessmentRecord) Recorded(TruthValue truth=TruthValue.Yes,EvidenceStatus evidence=EvidenceStatus.Present)
    {
        var pack=new KnowledgePackLoader().LoadFromFile(Path.Combine(AppContext.BaseDirectory,"Fixtures/demo-c-pack.json"));
        var intake=new NormalizedIntakeService(new UnusedStore()).Normalize(new(new("case-synthetic"),"synthetic-type",new("synthetic-source","synthetic-order","synthetic-message",1,"synthetic-adapter",1,new(2026,10,2,12,0,0,TimeSpan.Zero)),new(2026,10,2),
            new Dictionary<string,CaseValue>{["request_confirmed"]=truth,["measurement"]=15m,["alternative_confirmed"]=TruthValue.No},
            new Dictionary<string,EvidenceStatus>{["verification"]=evidence},new Dictionary<string,IReadOnlyList<string>>{["verification"]=evidence==EvidenceStatus.Missing?[]:["synthetic-document"]}),pack);
        var record=new AssessmentRecorder().Evaluate(pack,intake.Input.Facts,intake.Input.AssessmentDate,intake.Input.Evidence,new(new("assessment-synthetic"),intake.CaseId,"synthetic-platform",new(2026,10,2,12,1,0,TimeSpan.Zero)));
        return(intake,record);
    }
    private sealed class UnusedStore:INormalizedIntakeStore
    {
        public Task<IntakeReceipt> AppendAsync(NormalizedIntakeRecord record,CancellationToken cancellationToken=default)=>throw new InvalidOperationException("Normalization must not access a store.");
    }
}
