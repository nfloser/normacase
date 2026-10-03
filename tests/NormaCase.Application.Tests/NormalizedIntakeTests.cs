using System.Globalization;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
using NormaCase.Application.Intake;
using NormaCase.Domain.Cases;
using NormaCase.Domain.Decision;
using NormaCase.Domain.Evidence;
using NormaCase.Knowledge.Serialization;
using Xunit;

namespace NormaCase.Application.Tests;

public sealed class NormalizedIntakeTests
{
    [Fact]
    public void Different_synthetic_upstream_formats_normalize_to_the_same_internal_values()
    {
        var alpha = AdapterAlpha(File.ReadAllText(Fixture("intake-alpha.json")));
        var beta = AdapterBeta(File.ReadAllText(Fixture("intake-beta.xml")));
        var service = new NormalizedIntakeService(new MemoryStore());
        var a = service.Normalize(alpha, Pack());
        var b = service.Normalize(beta, Pack());
        Assert.Equal(a.Input.Facts, b.Input.Facts);
        Assert.Equal(a.Input.Evidence, b.Input.Evidence);
        Assert.Equal(15.1234567890123456789m, a.Input.Facts["measurement"].Number);
        Assert.Equal("synthetic-alpha", a.Provenance.SourceSystemId);
        Assert.Equal("synthetic-beta", b.Provenance.SourceSystemId);
        Assert.Equal("synthetic-order-1", a.Provenance.UpstreamCaseId);
        Assert.Equal("synthetic-document-1", a.EvidenceReferences["verification"].Single());
    }

    [Fact]
    public async Task Atomic_duplicate_delivery_returns_the_original_receipt_without_new_case()
    {
        var store = new MemoryStore();
        var service = new NormalizedIntakeService(store);
        var request = Request();
        var first = await service.AcceptAsync(request, Pack());
        var second = await service.AcceptAsync(request, Pack());
        Assert.Equal(IntakeAcceptance.Accepted, first.Acceptance);
        Assert.Equal(IntakeAcceptance.Duplicate, second.Acceptance);
        Assert.Same(first.Record, second.Record);
        Assert.Single(store.Records);
        var results = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => Task.Run(() => service.AcceptAsync(request, Pack()))));
        Assert.All(results, result => Assert.Equal(IntakeAcceptance.Duplicate, result.Acceptance));
    }

    [Fact]
    public async Task Same_message_or_upstream_revision_with_changed_values_conflicts()
    {
        var service = new NormalizedIntakeService(new MemoryStore());
        await service.AcceptAsync(Request(), Pack());
        await Assert.ThrowsAsync<IntakeConflictException>(() => service.AcceptAsync(Request(TruthValue.No), Pack()));
        await Assert.ThrowsAsync<IntakeConflictException>(() => service.AcceptAsync(Request(TruthValue.No, "another-message"), Pack()));
        var duplicate = await service.AcceptAsync(Request(TruthValue.Yes, "another-message"), Pack());
        Assert.Equal(IntakeAcceptance.Duplicate, duplicate.Acceptance);
    }

    [Fact]
    public async Task New_revision_is_separate_and_cannot_reuse_a_message_id()
    {
        var store = new MemoryStore();
        var service = new NormalizedIntakeService(store);
        await service.AcceptAsync(Request(), Pack());
        await Assert.ThrowsAsync<IntakeConflictException>(() => service.AcceptAsync(Request(revision: 2), Pack()));
        var updated = await service.AcceptAsync(Request(TruthValue.No, "revision-two", 2), Pack());
        Assert.Equal(IntakeAcceptance.Accepted, updated.Acceptance);
        Assert.Equal(2, store.Records.Count);
        Assert.Equal(TruthValue.Yes, store.Records[0].Input.Facts["request_confirmed"].Truth);
    }

    [Fact]
    public async Task Stale_revisions_and_changed_case_bindings_are_rejected()
    {
        var service = new NormalizedIntakeService(new MemoryStore());
        await service.AcceptAsync(Request(message: "revision-two", revision: 2), Pack());
        await Assert.ThrowsAsync<IntakeConflictException>(() => service.AcceptAsync(Request(message: "older", revision: 1), Pack()));
        var next = Request(message: "next", revision: 3);
        var remapped = new NormalizedIntakeRequest(new("another-case"), next.CaseTypeId, next.Provenance, next.AssessmentDate, next.Facts, next.Evidence, next.EvidenceReferences);
        await Assert.ThrowsAsync<IntakeConflictException>(() => service.AcceptAsync(remapped, Pack()));
    }

    [Fact]
    public async Task Independent_upstream_sources_cannot_claim_the_same_platform_case_identity()
    {
        var service = new NormalizedIntakeService(new MemoryStore());
        var alpha = AdapterAlpha(File.ReadAllText(Fixture("intake-alpha.json")));
        var beta = AdapterBeta(File.ReadAllText(Fixture("intake-beta.xml")));
        await service.AcceptAsync(alpha, Pack());
        await Assert.ThrowsAsync<IntakeConflictException>(() => service.AcceptAsync(beta, Pack()));
    }

    [Fact]
    public void Missing_values_and_evidence_are_explicit_and_caller_collections_are_detached()
    {
        var facts = new Dictionary<string, CaseValue>();
        var evidence = new Dictionary<string, EvidenceStatus>();
        var references = new Dictionary<string, IReadOnlyList<string>> { ["verification"] = new List<string>() };
        var request = new NormalizedIntakeRequest(new("case-synthetic"), "synthetic-type", Provenance(),
            new DateOnly(2026, 10, 2), facts, evidence, references);
        facts["request_confirmed"] = TruthValue.Yes;
        var record = new NormalizedIntakeService(new MemoryStore()).Normalize(request, Pack());
        Assert.True(record.Input.Facts["request_confirmed"].IsUnknown);
        Assert.Equal(EvidenceStatus.Missing, record.Input.Evidence["verification"]);
        Assert.Empty(record.EvidenceReferences["verification"]);
        Assert.Throws<NotSupportedException>(() => ((IDictionary<string, CaseValue>)record.Input.Facts).Clear());
    }

    [Fact]
    public void Undeclared_fields_wrong_types_and_evidence_references_fail_before_storage()
    {
        var service = new NormalizedIntakeService(new MemoryStore());
        var provenance = Provenance();
        Assert.Throws<ArgumentException>(() => service.Normalize(new(new("case"), "type", provenance,
            new(2026,10,2), new Dictionary<string,CaseValue>{["unknown"] = TruthValue.Yes}, new Dictionary<string,EvidenceStatus>(), new Dictionary<string,IReadOnlyList<string>>()), Pack()));
        Assert.Throws<ArgumentException>(() => service.Normalize(new(new("case"), "type", provenance,
            new(2026,10,2), new Dictionary<string,CaseValue>{["measurement"] = TruthValue.Yes}, new Dictionary<string,EvidenceStatus>(), new Dictionary<string,IReadOnlyList<string>>()), Pack()));
        Assert.Throws<ArgumentException>(() => service.Normalize(new(new("case"), "type", provenance,
            new(2026,10,2), new Dictionary<string,CaseValue>(), new Dictionary<string,EvidenceStatus>(), new Dictionary<string,IReadOnlyList<string>>{["unknown"] = ["doc"]}), Pack()));
    }

    [Fact]
    public void Boundary_limits_and_explicit_identity_time_are_enforced()
    {
        Assert.Throws<ArgumentException>(() => new IntakeProvenance("", "order", "message", 1, "adapter", 1, Utc()));
        Assert.Throws<ArgumentException>(() => new IntakeProvenance(new string('a',129), "order", "message", 1, "adapter", 1, Utc()));
        Assert.Throws<ArgumentOutOfRangeException>(() => new IntakeProvenance("source", "order", "message", 0, "adapter", 1, Utc()));
        Assert.Throws<ArgumentException>(() => new IntakeProvenance("source", "order", "message", 1, "adapter", 1, default));
        Assert.Throws<ArgumentException>(() => new NormalizedIntakeRequest(default, "type", Provenance(), new(2026,10,2), new Dictionary<string,CaseValue>(), new Dictionary<string,EvidenceStatus>(), new Dictionary<string,IReadOnlyList<string>>()));
        Assert.Throws<ArgumentException>(() => new NormalizedIntakeRequest(new("case"), "type", Provenance(), default, new Dictionary<string,CaseValue>(), new Dictionary<string,EvidenceStatus>(), new Dictionary<string,IReadOnlyList<string>>()));
        Assert.Throws<ArgumentException>(() => new NormalizedIntakeRequest(new("case"), "type", Provenance(), new(2026,10,2), Enumerable.Range(0,257).ToDictionary(i => "f"+i, _ => CaseValue.Unknown), new Dictionary<string,EvidenceStatus>(), new Dictionary<string,IReadOnlyList<string>>()));
        Assert.Throws<ArgumentException>(() => new NormalizedIntakeRequest(new("case"), "type", Provenance(), new(2026,10,2), new Dictionary<string,CaseValue>(), new Dictionary<string,EvidenceStatus>(), new Dictionary<string,IReadOnlyList<string>>{["verification"] = ["file:///patient-file"]}));
    }

    [Fact]
    public void Xml_fixture_adapter_prohibits_dtd_and_unknown_values_stay_unknown()
    {
        Assert.Throws<XmlException>(() => AdapterBeta("<!DOCTYPE x [<!ENTITY ext SYSTEM 'file:///etc/passwd'>]><SyntheticOrder/>"));
        var request = AdapterAlpha("{\"message\":\"message\",\"order\":\"order\",\"revision\":1,\"date\":\"2026-10-02\",\"answers\":{\"confirmed\":\"unknown\"},\"documents\":[]}");
        Assert.True(request.Facts["request_confirmed"].IsUnknown);
        Assert.Throws<ArgumentException>(() => AdapterAlpha("{\"message\":\"message\",\"order\":\"order\",\"revision\":1,\"date\":\"2026-10-02\",\"answers\":{\"confirmed\":\"maybe\"},\"documents\":[]}"));
    }

    private static NormalizedIntakeRequest Request(TruthValue truth = TruthValue.Yes, string message = "message", long revision = 1) => new(
        new("case-synthetic"), "synthetic-type", Provenance(message, revision), new(2026,10,2),
        new Dictionary<string,CaseValue>{["request_confirmed"] = truth, ["measurement"] = 15m, ["alternative_confirmed"] = TruthValue.No},
        new Dictionary<string,EvidenceStatus>{["verification"] = EvidenceStatus.Present},
        new Dictionary<string,IReadOnlyList<string>>{["verification"] = ["synthetic-document-1"]});
    private static IntakeProvenance Provenance(string message = "message", long revision = 1) => new("synthetic-source", "synthetic-order", message, revision, "synthetic-adapter", 1, Utc());
    private static DateTimeOffset Utc() => new(2026,10,2,12,0,0,TimeSpan.Zero);
    private static string Fixture(string name) => Path.Combine(AppContext.BaseDirectory,"Fixtures",name);
    private static NormaCase.Knowledge.Model.KnowledgePack Pack() => new KnowledgePackLoader().LoadFromFile(Fixture("demo-c-pack.json"));

    // These intentionally different synthetic fixture adapters are NOT MD interfaces.
    private static NormalizedIntakeRequest AdapterAlpha(string json)
    {
        using var document = JsonDocument.Parse(json, new JsonDocumentOptions{MaxDepth=8});
        var root = document.RootElement;
        return Map("synthetic-alpha", root.GetProperty("order").GetString()!, root.GetProperty("message").GetString()!, root.GetProperty("revision").GetInt64(),
            root.GetProperty("date").GetString()!, root.GetProperty("answers").EnumerateObject().ToDictionary(p=>p.Name,p=>p.Value.GetString()!), root.GetProperty("documents").EnumerateArray().Select(d=>d.GetString()!).ToArray());
    }
    private static NormalizedIntakeRequest AdapterBeta(string xml)
    {
        using var reader = XmlReader.Create(new StringReader(xml), new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=8192});
        var root = XDocument.Load(reader).Root!;
        return Map("synthetic-beta", root.Attribute("order")!.Value, root.Attribute("message")!.Value, long.Parse(root.Attribute("revision")!.Value,CultureInfo.InvariantCulture),
            root.Attribute("date")!.Value, root.Elements("Answer").ToDictionary(e=>e.Attribute("name")!.Value,e=>e.Attribute("value")!.Value), root.Elements("Attachment").Select(e=>e.Attribute("reference")!.Value).ToArray());
    }
    private static NormalizedIntakeRequest Map(string source,string order,string message,long revision,string date,Dictionary<string,string> answers,string[] docs)
    {
        var facts = new Dictionary<string,CaseValue>();
        if (answers.TryGetValue("confirmed",out var truth)) facts["request_confirmed"] = truth.ToUpperInvariant() switch {"YES"=>TruthValue.Yes,"NO"=>TruthValue.No,"UNKNOWN"=>TruthValue.Unknown,_=>throw new ArgumentException("Unmapped synthetic value.")};
        if (answers.TryGetValue("measurement",out var number)) facts["measurement"] = decimal.Parse(number,NumberStyles.AllowDecimalPoint|NumberStyles.AllowLeadingSign,CultureInfo.InvariantCulture);
        return new(new("case-synthetic"), "synthetic-type", new(source,order,message,revision,source+"-adapter",1,Utc()), DateOnly.ParseExact(date,"yyyy-MM-dd",CultureInfo.InvariantCulture), facts,
            new Dictionary<string,EvidenceStatus>{["verification"] = docs.Length>0?EvidenceStatus.Present:EvidenceStatus.Missing},new Dictionary<string,IReadOnlyList<string>>{["verification"] = docs});
    }

    private sealed class MemoryStore : INormalizedIntakeStore
    {
        public List<NormalizedIntakeRecord> Records { get; } = [];
        public Task<IntakeReceipt> AppendAsync(NormalizedIntakeRecord record,CancellationToken cancellationToken = default)
        {
            lock (Records)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var p=record.Provenance;
                if (Records.Any(r => r.CaseId == record.CaseId && (r.Provenance.SourceSystemId != p.SourceSystemId || r.Provenance.UpstreamCaseId != p.UpstreamCaseId))) throw new IntakeConflictException();
                var existing=Records.FirstOrDefault(r=>r.Provenance.SourceSystemId==p.SourceSystemId && (r.Provenance.MessageId==p.MessageId || (r.Provenance.UpstreamCaseId==p.UpstreamCaseId && r.Provenance.UpstreamRevision==p.UpstreamRevision)));
                if(existing is not null)
                {
                    if(!existing.HasSameContent(record)) throw new IntakeConflictException();
                    return Task.FromResult(new IntakeReceipt(IntakeAcceptance.Duplicate,existing));
                }
                var prior = Records.Where(r => r.Provenance.SourceSystemId == p.SourceSystemId && r.Provenance.UpstreamCaseId == p.UpstreamCaseId).ToArray();
                if (prior.Any(r => r.CaseId != record.CaseId || r.CaseTypeId != record.CaseTypeId) || prior.Any(r => r.Provenance.UpstreamRevision >= p.UpstreamRevision)) throw new IntakeConflictException();
                Records.Add(record);
                return Task.FromResult(new IntakeReceipt(IntakeAcceptance.Accepted,record));
            }
        }
    }
}
