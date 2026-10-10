using System.Net;
using System.Text.Json;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Mvc.Testing;
using NormaCase.Serialization;
using Xunit;

namespace NormaCase.Api.Tests;

public sealed class DocumentCaseTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient client;
    public DocumentCaseTests(WebApplicationFactory<Program> factory) => client = factory.CreateClient();

    [Theory]
    [InlineData("reference-transport-complete", "Supported")]
    [InlineData("reference-transport-missing", "Incomplete")]
    [InlineData("reference-transport-conflicting", "HumanReview")]
    [InlineData("reference-transport-negative", "NotSupported")]
    [InlineData("reference-care-complete", "Supported")]
    [InlineData("reference-care-missing", "Incomplete")]
    [InlineData("reference-care-out-of-range", "HumanReview")]
    [InlineData("reference-care-conflicting", "Incomplete")]
    public async Task Public_reference_documents_feed_existing_engine(string id, string outcome)
    {
        using var document = JsonDocument.Parse(await client.GetStringAsync("/api/document-cases/" + id));
        var root = document.RootElement;
        Assert.Equal("PUBLIC_REFERENCE", root.GetProperty("validationLevel").GetString());
        Assert.Equal(outcome, AssessmentJson.Deserialize(root.GetProperty("assessmentJson").GetString()!).Assessment.Outcome.ToString());
        Assert.NotEmpty(root.GetProperty("observations").EnumerateArray());
        Assert.NotEmpty(root.GetProperty("documents").EnumerateArray());
    }

    [Fact]
    public async Task Every_pitch_case_has_bound_documents_without_changing_results()
    {
        using var queues = JsonDocument.Parse(await client.GetStringAsync("/api/work-queues"));
        foreach (var item in queues.RootElement.GetProperty("queues").EnumerateArray().SelectMany(q => q.GetProperty("items").EnumerateArray()))
        {
            var id=item.GetProperty("caseId").GetString();
            using var file=JsonDocument.Parse(await client.GetStringAsync("/api/document-cases/"+id));
            using var original=JsonDocument.Parse(await client.GetStringAsync("/api/work-cases/"+id));
            Assert.Equal(original.RootElement.GetProperty("assessmentJson").GetString(),file.RootElement.GetProperty("assessmentJson").GetString());
            var docs=file.RootElement.GetProperty("documents").EnumerateArray().ToArray();
            Assert.NotEmpty(docs);
            foreach(var doc in docs)
            {
                var response=await client.GetAsync($"/api/document-cases/{id}/documents/{doc.GetProperty("id").GetString()}");
                Assert.Equal(HttpStatusCode.OK,response.StatusCode);
                Assert.Equal("no-store",response.Headers.CacheControl!.ToString());
                Assert.Equal(doc.GetProperty("sha256").GetString(),Convert.ToHexStringLower(SHA256.HashData(await response.Content.ReadAsByteArrayAsync())));
            }
        }
    }

    [Fact]
    public async Task Unknown_and_cross_case_documents_fail_and_preview_is_same_origin_only()
    {
        Assert.Equal(HttpStatusCode.NotFound,(await client.GetAsync("/api/document-cases/unknown")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await client.GetAsync("/api/document-cases/demo-g-review/documents/document-2")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await client.GetAsync("/api/document-cases/demo-g-supported/documents/catalog.json")).StatusCode);
        using var request=new HttpRequestMessage(HttpMethod.Get,"/api/document-cases/demo-g-supported/documents/document-1");
        request.Headers.Add("Origin","https://example.org");
        Assert.Equal(HttpStatusCode.Forbidden,(await client.SendAsync(request)).StatusCode);
        var preview=await client.GetAsync("/api/document-cases/demo-g-supported/documents/document-1");
        Assert.Equal("SAMEORIGIN",preview.Headers.GetValues("X-Frame-Options").Single());
        Assert.Contains("frame-ancestors 'self'",preview.Headers.GetValues("Content-Security-Policy").Single());
        var download=await client.GetAsync("/api/document-cases/demo-g-supported/documents/document-1?download=true");
        Assert.Equal("attachment",download.Content.Headers.ContentDisposition!.DispositionType);
    }
}
