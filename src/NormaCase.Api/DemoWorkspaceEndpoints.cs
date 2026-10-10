using System.Text.Json;
using System.Text.RegularExpressions;

namespace NormaCase.Api;

// Personal organization and explicit local rehearsal only. Never clinical approval or transmission.
internal static partial class DemoWorkspaceEndpoints
{
    internal sealed record Case(string CaseId,string Title,string QueueId,bool CanConfirm,string? AssessmentJson);
    private sealed record Folder(string Id,string Label,string Color);
    private sealed record Mark(string? FolderId=null,string? Color=null,bool Bookmark=false,string Note="",bool Confirmed=false,bool Dispatched=false);
    private sealed record Audit(int Sequence,string Action,string[] CaseIds,string? FolderId);
    internal sealed record Command(string Action,int ExpectedRevision,string[] CaseIds,string OperationId,
        string? FolderId=null,string? Label=null,string? Color=null,bool? Value=null,string? Note=null);
    [GeneratedRegex("^#[0-9a-fA-F]{6}$",RegexOptions.CultureInvariant)]
    private static partial Regex ColorPattern();
    internal static void Map(WebApplication app,Case[] source)
    {
        if(app.Configuration.GetValue<bool>("SyntheticReview:PersistenceEnabled"))return;
        var cases=source.ToDictionary(c=>c.CaseId,StringComparer.Ordinal);
        var folders=new Dictionary<string,Folder>(StringComparer.Ordinal);
        var marks=cases.Keys.ToDictionary(id=>id,_=>new Mark(),StringComparer.Ordinal);
        var queueColors=new Dictionary<string,string>(StringComparer.Ordinal);
        var audit=new List<Audit>();var revision=0;var sync=new object();
        var completed=new Dictionary<string,(string Request,string Response)>(StringComparer.Ordinal);
        object Snapshot()=>new{revision,localSimulation=true,folders=folders.Values.ToArray(),queueColors,
            cases=source.Select(c=>new{c.CaseId,c.Title,c.QueueId,c.CanConfirm,organization=marks[c.CaseId]}).ToArray(),audit=audit.ToArray()};
        app.MapGet("/api/demo-workspace",()=>{lock(sync)return Results.Json(Snapshot());});
        app.MapPost("/api/demo-workspace/commands",(Command command)=>
        {
            lock(sync)
            {
                var request=JsonSerializer.Serialize(command);
                if(string.IsNullOrWhiteSpace(command.OperationId)||command.OperationId.Length>100||command.CaseIds is null
                    ||command.CaseIds.Length>100||command.CaseIds.Distinct(StringComparer.Ordinal).Count()!=command.CaseIds.Length
                    ||command.CaseIds.Any(id=>id is null||!cases.ContainsKey(id)))return DemoHost.Error("invalid_input",400);
                if(completed.TryGetValue(command.OperationId,out var retained))
                    return retained.Request==request?Results.Content(retained.Response,"application/json"):DemoHost.Error("workspace_conflict",409);
                if(command.ExpectedRevision!=revision)return DemoHost.Error("workspace_conflict",409);
                if(audit.Count>=500)return DemoHost.Error("workspace_limit",409);
                var ids=command.CaseIds;var folderId=command.FolderId;string? packageJson=null;
                switch(command.Action)
                {
                    case "CREATE_FOLDER":
                        if(folders.Count>=50||string.IsNullOrWhiteSpace(command.Label)||command.Label.Trim().Length>80
                            ||command.Color is null||!ColorPattern().IsMatch(command.Color)
                            ||folders.Values.Any(f=>f.Label.Equals(command.Label.Trim(),StringComparison.OrdinalIgnoreCase)))return DemoHost.Error("invalid_input",400);
                        folderId="folder-"+(revision+1);folders.Add(folderId,new(folderId,command.Label.Trim(),command.Color));break;
                    case "UPDATE_FOLDER":
                        if(folderId is null||!folders.ContainsKey(folderId)||string.IsNullOrWhiteSpace(command.Label)||command.Label.Trim().Length>80
                            ||command.Color is null||!ColorPattern().IsMatch(command.Color)
                            ||folders.Values.Any(f=>f.Id!=folderId&&f.Label.Equals(command.Label.Trim(),StringComparison.OrdinalIgnoreCase)))return DemoHost.Error("invalid_input",400);
                        folders[folderId]=new(folderId,command.Label.Trim(),command.Color);break;
                    case "DELETE_FOLDER":
                        if(folderId is null||!folders.ContainsKey(folderId))return DemoHost.Error("invalid_input",400);
                        folders.Remove(folderId);foreach(var id in marks.Keys.ToArray())if(marks[id].FolderId==folderId)marks[id]=marks[id] with{FolderId=null};break;
                    case "QUEUE_COLOR":
                        if(folderId is null||!source.Any(c=>c.QueueId==folderId)||command.Color is null||!ColorPattern().IsMatch(command.Color))return DemoHost.Error("invalid_input",400);
                        queueColors[folderId]=command.Color;break;
                    case "MOVE":
                        if(ids.Length==0||(folderId is not null&&!folders.ContainsKey(folderId)))return DemoHost.Error("invalid_input",400);
                        foreach(var id in ids)marks[id]=marks[id] with{FolderId=folderId};break;
                    case "COLOR":
                        if(ids.Length==0||(command.Color is not null&&!ColorPattern().IsMatch(command.Color)))return DemoHost.Error("invalid_input",400);
                        foreach(var id in ids)marks[id]=marks[id] with{Color=command.Color};break;
                    case "BOOKMARK":
                        if(ids.Length==0||command.Value is null)return DemoHost.Error("invalid_input",400);
                        foreach(var id in ids)marks[id]=marks[id] with{Bookmark=command.Value.Value};break;
                    case "NOTE":
                        if(ids.Length!=1||command.Note is null||command.Note.Length>1000)return DemoHost.Error("invalid_input",400);
                        marks[ids[0]]=marks[ids[0]] with{Note=command.Note};break;
                    case "CONFIRM":
                        if(ids.Length==0||ids.Any(id=>!cases[id].CanConfirm||marks[id].Confirmed||marks[id].Dispatched))return DemoHost.Error("workspace_ineligible",409);
                        foreach(var id in ids)marks[id]=marks[id] with{Confirmed=true};break;
                    case "DISPATCH":
                        if(ids.Length==0||ids.Any(id=>!marks[id].Confirmed||marks[id].Dispatched))return DemoHost.Error("workspace_ineligible",409);
                        // Downloadable rehearsal envelope, retaining original exact assessment strings.
                        packageJson=JsonSerializer.Serialize(new{formatVersion=1,mode="SYNTHETIC_LOCAL_ONLY",dispatchId=command.OperationId,
                            items=ids.Select(id=>new{caseId=id,title=cases[id].Title,assessmentJson=cases[id].AssessmentJson}).ToArray()});
                        foreach(var id in ids)marks[id]=marks[id] with{Dispatched=true};break;
                    default:return DemoHost.Error("invalid_input",400);
                }
                revision++;audit.Add(new(revision,command.Action,ids,folderId));
                var response=JsonSerializer.Serialize(new{workspace=Snapshot(),packageJson},new JsonSerializerOptions(JsonSerializerDefaults.Web));
                completed.Add(command.OperationId,(request,response));return Results.Content(response,"application/json");
            }
        });
    }
}
