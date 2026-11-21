using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using NormaCase.Knowledge.Serialization;
using NormaCase.Serialization;
using NormaCase.Application.Assessments;

namespace NormaCase.Api;

// Read-only, locally retained SYNTHETIC document corpus. Never a general upload or OCR endpoint.
internal static class DocumentCaseEndpoints
{
    internal static void Map(WebApplication app, string platformVersion,
        Func<string, string?> originalAssessment)
    {
        var root = Path.Combine(AppContext.BaseDirectory, "DocumentCases");
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
        var catalog = JsonSerializer.Deserialize<Catalog>(File.ReadAllText(Path.Combine(root,"catalog.json")),options)
            ?? throw new InvalidOperationException("Missing synthetic document catalog.");
        if(catalog.FormatVersion!=1 || catalog.Cases.Length>200)
            throw new InvalidOperationException("Invalid synthetic document catalog.");
        var sources=catalog.Sources.ToDictionary(x=>x.Id,StringComparer.Ordinal);
        var workspaceCases=new List<DemoWorkspaceEndpoints.Case>();
        var cases=new Dictionary<string,object>(StringComparer.Ordinal);
        var pageContent=new Dictionary<(string,string,int),byte[]>();
        var content=new Dictionary<(string,string),(byte[] Bytes,Document Metadata)>();
        foreach(var entry in catalog.Cases)
        {
            if(!SafeId(entry.CaseId)||entry.Documents.Length>10||!sources.ContainsKey(entry.SourceId))
                throw new InvalidOperationException("Invalid synthetic document identity.");
            if(entry.CaseId.StartsWith("reference-",StringComparison.Ordinal)
                &&(entry.Context is null||new[]{entry.Context.Request,entry.Context.Question,entry.Context.Background}
                    .Any(value=>string.IsNullOrWhiteSpace(value)||value.Length>5000)))
                throw new InvalidOperationException("Missing or invalid synthetic case context.");
            var directory=Path.Combine(root,entry.CaseId);
            var inputBytes=File.ReadAllBytes(Path.Combine(directory,"input.json"));
            Verify(inputBytes,entry.InputSha256);
            var inputJson=System.Text.Encoding.UTF8.GetString(inputBytes);
            var input=CaseInputJson.Deserialize(inputJson);
            foreach(var doc in entry.Documents)
            {
                if(!SafeId(doc.Id)||Path.GetFileName(doc.Filename)!=doc.Filename||doc.Pages<1
                    ||!sources.ContainsKey(doc.SourceId)
                    ||!(doc.MediaType=="application/pdf"&&doc.Filename==doc.Id+".pdf"
                        ||doc.MediaType=="image/png"&&doc.Filename==doc.Id+".png"
                        ||doc.MediaType=="text/plain"&&doc.Filename==doc.Id+".txt"))
                    throw new InvalidOperationException("Invalid synthetic document metadata.");
                var bytes=File.ReadAllBytes(Path.Combine(directory,doc.Filename));
                Verify(bytes,doc.Sha256);
                if(bytes.Length>2*1024*1024)throw new InvalidOperationException("Document exceeds demo limit.");
                content.Add((entry.CaseId,doc.Id),(bytes,doc));
                var previews=doc.Previews??[];
                if((doc.MediaType=="application/pdf"?doc.Pages:0)!=previews.Length||doc.Pages>20)
                    throw new InvalidOperationException("Invalid document previews.");
                foreach(var preview in previews)
                {
                    if(preview.Page<1||preview.Page>doc.Pages||preview.Filename!=doc.Id+"-page-"+preview.Page+".png")
                        throw new InvalidOperationException("Invalid page identity.");
                    var image=File.ReadAllBytes(Path.Combine(directory,preview.Filename));
                    Verify(image,preview.Sha256);
                    if(image.Length>2*1024*1024||!image.AsSpan().StartsWith(new byte[]{137,80,78,71,13,10,26,10}))
                        throw new InvalidOperationException("Invalid page image.");
                    pageContent.Add((entry.CaseId,doc.Id,preview.Page),image);
                }
            }
            foreach(var observation in entry.Observations)
            {
                var doc=entry.Documents.SingleOrDefault(x=>x.Id==observation.Id);
                if(doc is null||observation.Page<1||observation.Page>doc.Pages
                    ||doc.MediaType!="application/pdf"||observation.Method!="CONTROLLED_TEXT"
                    ||!input.Facts.ContainsKey(observation.Field))
                    throw new InvalidOperationException("Invalid synthetic document provenance.");
            }
            foreach(var field in input.Facts)
            {
                var observations=entry.Observations.Where(x=>x.Field==field.Key).Select(x=>x.Value).Distinct(StringComparer.Ordinal).ToArray();
                if(observations.Length==0)throw new InvalidOperationException("Missing document field provenance.");
                var value=observations.Length==1?observations[0]:"UNKNOWN";
                var expected=value switch
                {
                    "UNKNOWN"=>NormaCase.Domain.Cases.CaseValue.Unknown,
                    "YES"=>NormaCase.Domain.Cases.CaseValue.FromTruth(NormaCase.Domain.Decision.TruthValue.Yes),
                    "NO"=>NormaCase.Domain.Cases.CaseValue.FromTruth(NormaCase.Domain.Decision.TruthValue.No),
                    "NOT_APPLICABLE"=>NormaCase.Domain.Cases.CaseValue.FromTruth(NormaCase.Domain.Decision.TruthValue.NotApplicable),
                    _=>NormaCase.Domain.Cases.CaseValue.FromNumber(decimal.Parse(value,System.Globalization.CultureInfo.InvariantCulture))
                };
                if(field.Value!=expected)throw new InvalidOperationException("Document observations differ from normalized input.");
            }
            string? assessmentJson=null;
            var validation="SYNTHETIC";
            if(entry.PackPath is not null)
            {
                // Paths are an explicit local allowlist, not caller-controlled filesystem paths.
                var path=entry.PackPath switch
                {
                    "demo-g"=>Path.Combine(AppContext.BaseDirectory,"Knowledge","demo-g.json"),
                    "kt-rl-8-3"=>Path.Combine(AppContext.BaseDirectory,"ReferenceKnowledge","kt-rl-8-3.json"),
                    "pflege-adult-score"=>Path.Combine(AppContext.BaseDirectory,"ReferenceKnowledge","pflege-adult-score.json"),
                    _=>throw new InvalidOperationException("Unknown document fixture knowledge.")
                };
                var pack=new KnowledgePackLoader().LoadFromJson(File.ReadAllText(path));
                validation=pack.Manifest.ValidationLevel;
                var record=new AssessmentRecorder().Evaluate(pack,input.Facts,input.AssessmentDate,input.Evidence,
                    new(new("assessment-"+entry.CaseId),new(entry.CaseId),platformVersion,
                        new DateTimeOffset(2026,10,3,13,0,0,TimeSpan.Zero)));
                assessmentJson=AssessmentJson.Serialize(record.Result,record.PlatformVersion);
                if(record.Result.Outcome.ToString().Replace("_", "",StringComparison.Ordinal)
                    .ToUpperInvariant()!=entry.ExpectedOutcome?.Replace("_","",StringComparison.Ordinal))
                    throw new InvalidOperationException($"Synthetic fixture {entry.CaseId}: {record.Result.Outcome} differs from {entry.ExpectedOutcome}.");
                if(entry.PackPath=="demo-g"&&originalAssessment(entry.CaseId)!=assessmentJson)
                    throw new InvalidOperationException("Document fixture differs from original pitch case.");
            }
            var queueId=entry.PackPath is null?"documents":entry.ExpectedOutcome switch
            {
                "SUPPORTED" or "NOT_SUPPORTED"=>"approval", "INCOMPLETE"=>"clarification", "HUMAN_REVIEW"=>"review", _=>"technical"
            };
            if(entry.CaseId.StartsWith("demo-",StringComparison.Ordinal)&&entry.PackPath is null)queueId="technical";
            workspaceCases.Add(new(entry.CaseId,entry.Title,queueId,assessmentJson is not null&&entry.Findings.Length==0
                &&entry.ExpectedOutcome is "SUPPORTED" or "NOT_SUPPORTED",assessmentJson));
            cases.Add(entry.CaseId,new {entry.CaseId,entry.Title,entry.Scope,entry.Context,validationLevel=validation,
                entry.Documents,entry.Observations,entry.Findings,entry.FieldLabels,entry.EvidenceLabels,entry.OutputLabels,source=sources[entry.SourceId],
                assessmentJson,inputJson});
        }
        DemoWorkspaceEndpoints.Map(app,workspaceCases.ToArray());
        app.MapGet("/api/document-cases",()=>Results.Json(new {sources=catalog.Sources,
            cases=catalog.Cases.Where(x=>x.CaseId.StartsWith("reference-",StringComparison.Ordinal))
                .Select(x=>new{x.CaseId,x.Title,x.Scope,x.Context})}));
        app.MapGet("/api/document-cases/{caseId}",(string caseId)=>cases.TryGetValue(caseId,out var item)
            ?Results.Json(item):DemoHost.Error("unknown_work_case",404));
        app.MapGet("/api/document-cases/{caseId}/documents/{documentId}/pages/{page:int}",
            (string caseId,string documentId,int page)=>pageContent.TryGetValue((caseId,documentId,page),out var image)
                ?Results.File(image,"image/png"):DemoHost.Error("unknown_work_case",404));
        app.MapGet("/api/document-cases/{caseId}/documents/{documentId}",
            (HttpContext context,string caseId,string documentId,bool? download)=>
            {
                if(!content.TryGetValue((caseId,documentId),out var file))return DemoHost.Error("unknown_work_case",404);
                // Only vetted local PDF/PNG bytes may be embedded; the application remains unframeable.
                context.Response.Headers["X-Frame-Options"]="SAMEORIGIN";
                context.Response.Headers["Content-Security-Policy"]="default-src 'none'; frame-ancestors 'self'; base-uri 'none'";
                return Results.File(file.Bytes,file.Metadata.MediaType,
                    fileDownloadName:download==true?caseId+"-"+file.Metadata.Filename:null,
                    enableRangeProcessing:true);
            });
    }
    private static bool SafeId(string id)=>id.Length is >0 and <=100
        &&id.All(c=>c is >= 'a' and <= 'z' or >= '0' and <= '9' or '-');
    private static void Verify(byte[] bytes,string expected)
    {
        if(Convert.ToHexStringLower(SHA256.HashData(bytes))!=expected)
            throw new InvalidOperationException("Synthetic document integrity mismatch.");
    }
    private sealed record Catalog(int FormatVersion,Source[] Sources,Entry[] Cases);
    private sealed record Source(string Id,string Title,string Version,string Url,string Scope);
    private sealed record Entry(string CaseId,string Title,string? PackPath,string SourceId,string Scope,
        CaseContext? Context,string? ExpectedOutcome,string InputSha256,Document[] Documents,Observation[] Observations,string[] Findings,IReadOnlyDictionary<string,string> FieldLabels,IReadOnlyDictionary<string,string> EvidenceLabels,IReadOnlyDictionary<string,OutputLabel> OutputLabels);
    private sealed record CaseContext(string Request,string Question,string Background);
    private sealed record OutputLabel(string Label,IReadOnlyDictionary<string,string> Choices);
    private sealed record Document(string Id,string Title,string Filename,string MediaType,string Sha256,int Pages,string SourceId,Preview[]? Previews);
    private sealed record Preview(int Page,string Filename,string Sha256);
    private sealed record Observation(string Id,int Page,string Field,string Value,string Method);
}
