using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace NormaCase.Api.Tests;

public sealed class DemoWorkspaceTests
{
    [Fact]
    public async Task Personal_organization_never_changes_assessment_and_batches_are_revision_checked()
    {
        await using var factory=new WebApplicationFactory<Program>();using var client=factory.CreateClient();
        var original=await client.GetStringAsync("/api/document-cases/reference-md-mueller");
        async Task<JsonElement> State()=>JsonDocument.Parse(await client.GetStringAsync("/api/demo-workspace")).RootElement.Clone();
        var state=await State();var revision=state.GetProperty("revision").GetInt32();
        var created=await client.PostAsJsonAsync("/api/demo-workspace/commands",new{action="CREATE_FOLDER",expectedRevision=revision,label="Heute prüfen",color="#2563eb",caseIds=Array.Empty<string>(),operationId="create-folder"});
        Assert.Equal(HttpStatusCode.OK,created.StatusCode);state=await State();
        var folder=state.GetProperty("folders")[0].GetProperty("id").GetString();
        var moved=await client.PostAsJsonAsync("/api/demo-workspace/commands",new{action="MOVE",expectedRevision=state.GetProperty("revision").GetInt32(),folderId=folder,caseIds=new[]{"reference-md-mueller"},operationId="move"});
        Assert.Equal(HttpStatusCode.OK,moved.StatusCode);
        Assert.Equal(original,await client.GetStringAsync("/api/document-cases/reference-md-mueller"));
        var stale=await client.PostAsJsonAsync("/api/demo-workspace/commands",new{action="BOOKMARK",expectedRevision=revision,caseIds=new[]{"reference-md-mueller"},value=true,operationId="stale"});
        Assert.Equal(HttpStatusCode.Conflict,stale.StatusCode);
        state=await State();revision=state.GetProperty("revision").GetInt32();
        var invalid=await client.PostAsJsonAsync("/api/demo-workspace/commands",new{action="CONFIRM",expectedRevision=revision,caseIds=new[]{"reference-md-mueller","reference-accident-missing"},operationId="invalid-batch"});
        Assert.Equal(HttpStatusCode.Conflict,invalid.StatusCode);
        Assert.Equal(revision,(await State()).GetProperty("revision").GetInt32());
        var confirmed=await client.PostAsJsonAsync("/api/demo-workspace/commands",new{action="CONFIRM",expectedRevision=revision,caseIds=new[]{"reference-md-mueller"},operationId="confirm"});
        Assert.Equal(HttpStatusCode.OK,confirmed.StatusCode);
        var retry=await client.PostAsJsonAsync("/api/demo-workspace/commands",new{action="CONFIRM",expectedRevision=revision,caseIds=new[]{"reference-md-mueller"},operationId="confirm"});
        Assert.Equal(HttpStatusCode.OK,retry.StatusCode);Assert.Equal(await confirmed.Content.ReadAsStringAsync(),await retry.Content.ReadAsStringAsync());
        state=await State();
        var dispatch=await client.PostAsJsonAsync("/api/demo-workspace/commands",new{action="DISPATCH",expectedRevision=state.GetProperty("revision").GetInt32(),caseIds=new[]{"reference-md-mueller"},operationId="dispatch"});
        Assert.Equal(HttpStatusCode.OK,dispatch.StatusCode);
        var result=JsonDocument.Parse(await dispatch.Content.ReadAsStringAsync()).RootElement;
        Assert.Contains("SYNTHETIC_LOCAL_ONLY",result.GetProperty("packageJson").GetString());
        Assert.Equal(original,await client.GetStringAsync("/api/document-cases/reference-md-mueller"));
    }

    [Fact]
    public async Task Colors_bookmarks_notes_and_folder_deletion_preserve_cases()
    {
        await using var factory=new WebApplicationFactory<Program>();using var client=factory.CreateClient();
        var revision=0;
        async Task<JsonElement> Change(string action,string[] ids,object extra)
        {
            var payload=JsonSerializer.SerializeToElement(extra).EnumerateObject().ToDictionary(p=>p.Name,p=>(object?)p.Value);
            payload["action"]=action;payload["caseIds"]=ids;payload["expectedRevision"]=revision;payload["operationId"]="personal-"+revision;
            var response=await client.PostAsJsonAsync("/api/demo-workspace/commands",payload);Assert.Equal(HttpStatusCode.OK,response.StatusCode);
            var state=JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("workspace").Clone();revision=state.GetProperty("revision").GetInt32();return state;
        }
        var ids=new[]{"reference-md-mueller"};
        var state=await Change("CREATE_FOLDER",Array.Empty<string>(),new{label="Besprechung",color="#ffffff"});
        var folder=state.GetProperty("folders")[0].GetProperty("id").GetString();
        await Change("MOVE",ids,new{folderId=folder});
        await Change("COLOR",ids,new{color="#000000"});
        await Change("BOOKMARK",ids,new{value=true});
        state=await Change("NOTE",ids,new{note="Befund prüfen"});
        JsonElement Mark(JsonElement s)=>s.GetProperty("cases").EnumerateArray().Single(c=>c.GetProperty("caseId").GetString()==ids[0]).GetProperty("organization");
        Assert.Equal("#000000",Mark(state).GetProperty("color").GetString());Assert.True(Mark(state).GetProperty("bookmark").GetBoolean());Assert.Equal("Befund prüfen",Mark(state).GetProperty("note").GetString());
        state=await Change("QUEUE_COLOR",Array.Empty<string>(),new{folderId="approval",color="#ff00ff"});Assert.Equal("#ff00ff",state.GetProperty("queueColors").GetProperty("approval").GetString());
        state=await Change("DELETE_FOLDER",Array.Empty<string>(),new{folderId=folder});Assert.Equal(JsonValueKind.Null,Mark(state).GetProperty("folderId").ValueKind);Assert.Equal(122,state.GetProperty("cases").GetArrayLength());
        state=await Change("COLOR",ids,new{color=(string?)null});Assert.Equal(JsonValueKind.Null,Mark(state).GetProperty("color").ValueKind);
    }

    [Fact]
    public async Task Organization_rejects_unknown_cases_invalid_colors_and_external_origins()
    {
        await using var factory=new WebApplicationFactory<Program>();using var client=factory.CreateClient();
        foreach(var color in new[]{"red","#fff","url(x)"})
            Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsJsonAsync("/api/demo-workspace/commands",new{action="CREATE_FOLDER",expectedRevision=0,label="Test",color,caseIds=Array.Empty<string>(),operationId="color-"+color})).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsJsonAsync("/api/demo-workspace/commands",new{action="BOOKMARK",expectedRevision=0,caseIds=new[]{"unknown"},operationId="unknown",value=true})).StatusCode);
        using var request=new HttpRequestMessage(HttpMethod.Get,"/api/demo-workspace");request.Headers.Add("Origin","https://example.org");
        Assert.Equal(HttpStatusCode.Forbidden,(await client.SendAsync(request)).StatusCode);
    }
}
