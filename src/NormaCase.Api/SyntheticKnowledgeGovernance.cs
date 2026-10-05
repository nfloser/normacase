using System.Globalization;
using System.Text;
using System.Text.Json;
using NormaCase.Application.Knowledge;
using NormaCase.Application.Reviews;
using NormaCase.Persistence.PostgreSql;

namespace NormaCase.Api;

internal sealed class SyntheticKnowledgePermissions
{
    private readonly Dictionary<string, string> assignments = new(StringComparer.Ordinal);
    internal SyntheticKnowledgePermissions(IConfiguration configuration, SyntheticReviewCredential credential)
    {
        var section = configuration.GetSection("SyntheticReview:KnowledgeAdministration");
        foreach (var item in section.GetChildren())
        {
            if (item.Key is not ("PROPOSE" or "REVIEW" or "ACTIVATE")
                || item.Value is null || !credential.IsConfiguredUser(item.Value))
                throw new InvalidOperationException("Invalid synthetic Knowledge administration configuration.");
            assignments.Add(item.Key, item.Value);
        }
        if (assignments.TryGetValue("PROPOSE", out var proposer)
            && assignments.TryGetValue("REVIEW", out var reviewer) && proposer == reviewer)
            throw new InvalidOperationException("Synthetic Knowledge proposer and reviewer must be distinct.");
    }
    internal string[] Actions(AuthenticatedReviewActor actor)
        => actor.AuthenticationAuthority == "synthetic-local"
            ? assignments.Where(pair => pair.Value == actor.ActorId).Select(pair => pair.Key).Order().ToArray() : [];
}

internal static class SyntheticKnowledgeGovernance
{
    internal static void Map(WebApplication app)
    {
        var permissions = app.Services.GetRequiredService<SyntheticKnowledgePermissions>();
        var source = app.Services.GetRequiredService<Npgsql.NpgsqlDataSource>();
        var store = new PostgresReviewedKnowledgeActivationStore(source, requireRetainedEvidence: true);
        var releases = new PostgresKnowledgeReleaseStore(source);
        var evidenceStore = new PostgresKnowledgeEvidenceStore(source);
        var entitlements = app.Services.GetRequiredService<SyntheticLiveEntitlements>();
        var group = app.MapGroup("/api/review/knowledge").RequireAuthorization();
        group.AddEndpointFilter(async (invocation, next) =>
        {
            var context = invocation.HttpContext;
            var actor = Actor(context);
            if (permissions.Actions(actor).Length == 0) return DemoHost.Error("review_forbidden", 403);
            // Role checks remain inside each endpoint; the scope only binds live account
            // authority and store writes to one backend, without deriving Knowledge roles
            // from case grants or the requesting actor.
            return await entitlements.ExecuteAuthorizedAsync<object?>(actor,
                async (_, _) => await next(invocation), context.RequestAborted);
        });

        var imports = new PostgresKnowledgeReleaseImportStore(source);
        group.MapGet("/releases", async (HttpContext context, CancellationToken token) =>
        {
            if (!Allowed(context, permissions)) return DemoHost.Error("review_forbidden", 403);
            if (context.Request.Query.Keys.Any(key => key is not ("packId" or "afterPackId" or "afterReleaseId"))
                || context.Request.Query.Any(pair => pair.Value.Count != 1)) return DemoHost.Error("invalid_input", 400);
            try
            {
                string? Query(string key) => context.Request.Query.ContainsKey(key) ? context.Request.Query[key].ToString() : null;
                var page = await releases.ListAsync(25, Query("packId"), Query("afterPackId"), Query("afterReleaseId"), "SYNTHETIC", token);
                return Results.Json(new { releases = page.Take(25).Select(Release),
                    nextPageCursor = page.Count > 25 ? new { packId = page[24].PackId, releaseId = page[24].ReleaseId } : null });
            }
            catch (ArgumentException) { return DemoHost.Error("invalid_input", 400); }
        });
        group.MapGet("/release", async (HttpContext context, CancellationToken token) =>
        {
            if (!Allowed(context, permissions)) return DemoHost.Error("review_forbidden", 403);
            if (context.Request.Query.Count != 2 || !context.Request.Query.ContainsKey("packId") || !context.Request.Query.ContainsKey("releaseId")
                || context.Request.Query.Any(pair => pair.Value.Count != 1)) return DemoHost.Error("invalid_input", 400);
            try
            {
                var artifact = await releases.LoadAsync(context.Request.Query["packId"].ToString(), context.Request.Query["releaseId"].ToString(), token);
                if (artifact is null || artifact.ValidationLevel != "SYNTHETIC") return DemoHost.Error("unknown_knowledge_change", 404);
                return Results.Json(ReleaseDetail(artifact, await imports.LoadAsync(artifact.PackId, artifact.ReleaseId, token)));
            }
            catch (ArgumentException) { return DemoHost.Error("invalid_input", 400); }
        });
        group.MapPost("/releases", async (HttpContext context, CancellationToken token) =>
        {
            if (!Allowed(context, permissions, "PROPOSE")) return DemoHost.Error("review_forbidden", 403);
            using var json = await Body(context.Request, ["packJson"], token, 397312);
            if (json is null) return DemoHost.Error("invalid_input", 400);
            try
            {
                var exact = Text(json.RootElement, "packJson");
                if (new UTF8Encoding(false, true).GetByteCount(exact) > 65536) return DemoHost.Error("input_too_large", 413);
                var candidate = new NormaCase.Knowledge.Catalog.KnowledgeReleaseCatalog().Register(exact);
                if (candidate.ValidationLevel != "SYNTHETIC") return DemoHost.Error("review_forbidden", 403);
                var imported = await imports.ImportAsync(exact, Now(), token);
                return Results.Json(ReleaseDetail(imported.Artifact, imported));
            }
            catch (NormaCase.Knowledge.Catalog.KnowledgeReleaseIdentityConflictException) { return DemoHost.Error("knowledge_change_conflict", 409); }
            catch (Exception exception) when (exception is ArgumentException or JsonException or NormaCase.Knowledge.Validation.KnowledgeValidationException)
            { return DemoHost.Error("invalid_input", 400); }
        });

        group.MapPost("/evidence", async (HttpContext context, CancellationToken token) =>
        {
            if (!Allowed(context, permissions, "PROPOSE")) return DemoHost.Error("review_forbidden", 403);
            using var json = await Body(context.Request, ["content", "kind", "title"], token, 397312);
            if (json is null) return DemoHost.Error("invalid_input", 400);
            try
            {
                var artifact = new KnowledgeEvidenceArtifact("synthetic-evidence-" + Guid.NewGuid().ToString("N"),
                    Text(json.RootElement, "kind"), Text(json.RootElement, "title"), Text(json.RootElement, "content"),
                    Actor(context).ActorId, Now());
                return Results.Json(Evidence(await evidenceStore.RegisterAsync(artifact, token)));
            }
            catch (ArgumentException) { return DemoHost.Error("invalid_input", 400); }
        });

        group.MapGet("/changes", async (HttpContext context, CancellationToken token) =>
        {
            if (!Allowed(context, permissions)) return DemoHost.Error("review_forbidden", 403);
            var cursor = context.Request.Query["afterChangeId"].ToString();
            if (context.Request.Query.Keys.Any(key => key != "afterChangeId") || context.Request.Query["afterChangeId"].Count > 1)
                return DemoHost.Error("invalid_input", 400);
            try
            {
                var records = await store.ListChangesAsync(26, cursor.Length == 0 ? null : cursor, token);
                return Results.Json(new { changes = records.Take(25).Select(Change).ToArray(),
                    nextPageCursor = records.Count > 25 ? records[24].Proposal.ChangeId : null });
            }
            catch (ArgumentException) { return DemoHost.Error("invalid_input", 400); }
        });
        group.MapGet("/changes/{changeId}", async (string changeId, HttpContext context, CancellationToken token) =>
        {
            if (!Allowed(context, permissions)) return DemoHost.Error("review_forbidden", 403);
            try
            {
                var change = await store.LoadChangeAsync(changeId, token);
                if (change is null) return DemoHost.Error("unknown_knowledge_change", 404);
                var active = await store.LoadActiveAsync(change.Proposal.PackId, token);
                var evidence = new List<object>();
                var retainedEvidenceComplete = true;
                foreach (var (id, kind) in new[] { (change.Proposal.SourceReference, "SOURCE"), (change.Proposal.ImpactReference, "IMPACT"), (change.Proposal.TestReference, "TESTS") })
                {
                    var retained = await evidenceStore.LoadAsync(id, token);
                    retainedEvidenceComplete &= retained is not null && retained.Kind == kind && retained.RecordedAtUtc <= change.Proposal.ProposedAtUtc;
                    if (retained is not null) evidence.Add(Evidence(retained));
                }
                return Results.Json(new { change = Change(change), active = Activation(active), evidence, retainedEvidenceComplete });
            }
            catch (ArgumentException) { return DemoHost.Error("invalid_input", 400); }
        });
        group.MapPost("/changes", async (HttpContext context, CancellationToken token) =>
        {
            if (!Allowed(context, permissions, "PROPOSE")) return DemoHost.Error("review_forbidden", 403);
            using var json = await Body(context.Request, ["impactReference", "packId", "releaseId", "sourceReference", "testReference"], token);
            if (json is null) return DemoHost.Error("invalid_input", 400);
            try
            {
                var root = json.RootElement;
                var artifact = await releases.LoadAsync(Text(root, "packId"), Text(root, "releaseId"), token);
                if (artifact is null || artifact.ValidationLevel != "SYNTHETIC")
                    return DemoHost.Error("unknown_knowledge_change", 404);
                var result = await store.ProposeAsync(new("synthetic-change-" + Guid.NewGuid().ToString("N"),
                    artifact.PackId, artifact.ReleaseId, artifact.Sha256, Text(root, "sourceReference"),
                    Text(root, "impactReference"), Text(root, "testReference"), Actor(context).ActorId, Now()), token);
                return Results.Json(Change(result));
            }
            catch (KnowledgeGovernanceConflictException) { return DemoHost.Error("knowledge_evidence_required", 409); }
            catch (ArgumentException) { return DemoHost.Error("invalid_input", 400); }
        });
        group.MapPost("/changes/{changeId}/decision", async (string changeId, HttpContext context, CancellationToken token) =>
        {
            if (!Allowed(context, permissions, "REVIEW")) return DemoHost.Error("review_forbidden", 403);
            using var json = await Body(context.Request, ["approved", "reason"], token);
            if (json is null) return DemoHost.Error("invalid_input", 400);
            try
            {
                if (json.RootElement.GetProperty("approved").ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                    return DemoHost.Error("invalid_input", 400);
                if (!await SyntheticChange(store, releases, changeId, token)) return DemoHost.Error("unknown_knowledge_change", 404);
                return Results.Json(Change(await store.DecideAsync(new(changeId, Actor(context).ActorId, Now(),
                    json.RootElement.GetProperty("approved").GetBoolean(), Text(json.RootElement, "reason")), token)));
            }
            catch (ArgumentException) { return DemoHost.Error("invalid_input", 400); }
            catch (KnowledgeGovernanceConflictException) { return DemoHost.Error("knowledge_change_conflict", 409); }
        });
        group.MapPost("/changes/{changeId}/activate", async (string changeId, HttpContext context, CancellationToken token) =>
        {
            if (!Allowed(context, permissions, "ACTIVATE")) return DemoHost.Error("review_forbidden", 403);
            using var json = await Body(context.Request, ["expectedRevision"], token);
            if (json is null) return DemoHost.Error("invalid_input", 400);
            try
            {
                var raw = Text(json.RootElement, "expectedRevision");
                if (!long.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var revision))
                    return DemoHost.Error("invalid_input", 400);
                if (!await SyntheticChange(store, releases, changeId, token)) return DemoHost.Error("unknown_knowledge_change", 404);
                return Results.Json(Activation(await store.ActivateAsync(new(changeId, revision, Actor(context).ActorId, Now()), token)));
            }
            catch (ArgumentException) { return DemoHost.Error("invalid_input", 400); }
            catch (KnowledgeGovernanceConflictException) { return DemoHost.Error("knowledge_change_conflict", 409); }
        });
    }
    private static async Task<bool> SyntheticChange(PostgresReviewedKnowledgeActivationStore store,
        PostgresKnowledgeReleaseStore releases, string changeId, CancellationToken token)
    {
        var change = await store.LoadChangeAsync(changeId, token);
        return change is not null && (await releases.LoadAsync(change.Proposal.PackId, change.Proposal.ReleaseId, token))?.ValidationLevel == "SYNTHETIC";
    }
    private static AuthenticatedReviewActor Actor(HttpContext context) => SyntheticReviewAuthentication.ResolveActor(context.User);
    private static bool Allowed(HttpContext context, SyntheticKnowledgePermissions permissions, string? action = null)
    {
        var actions = permissions.Actions(Actor(context));
        return action is null ? actions.Length > 0 : actions.Contains(action);
    }
    private static object Change(KnowledgeChangeRecord record) => new
    {
        changeId = record.Proposal.ChangeId, packId = record.Proposal.PackId, releaseId = record.Proposal.ReleaseId,
        sha256 = record.Proposal.Sha256, sourceReference = record.Proposal.SourceReference,
        impactReference = record.Proposal.ImpactReference, testReference = record.Proposal.TestReference,
        proposerActorId = record.Proposal.ProposerActorId, proposedAtUtc = record.Proposal.ProposedAtUtc,
        decision = record.Decision is null ? null : new { reviewerActorId = record.Decision.ReviewerActorId,
            reviewedAtUtc = record.Decision.ReviewedAtUtc, approved = record.Decision.Approved, reason = record.Decision.Reason }
    };
    private static object? Activation(KnowledgeActivationRecord? record) => record is null ? null : new
    {
        packId = record.PackId, revision = record.Revision.ToString(CultureInfo.InvariantCulture),
        changeId = record.ChangeId, releaseId = record.ReleaseId, sha256 = record.Sha256,
        actorId = record.ActorId, activatedAtUtc = record.ActivatedAtUtc
    };
    private static object Release(NormaCase.Knowledge.Catalog.KnowledgeReleaseArtifact artifact) => new
    {
        packId = artifact.PackId, releaseId = artifact.ReleaseId, sha256 = artifact.Sha256,
        lifecycleStatus = artifact.LifecycleStatus, validationLevel = artifact.ValidationLevel
    };
    private static object ReleaseDetail(NormaCase.Knowledge.Catalog.KnowledgeReleaseArtifact artifact, KnowledgeReleaseImportRecord? imported) => new
    {
        packId = artifact.PackId, releaseId = artifact.ReleaseId, sha256 = artifact.Sha256,
        lifecycleStatus = artifact.LifecycleStatus, validationLevel = artifact.ValidationLevel,
        packJson = artifact.KnowledgePackJson, import = imported is null ? null : new
        { importedByActorId = imported.ImportedByActorId, importedAtUtc = imported.ImportedAtUtc }
    };
    private static object Evidence(KnowledgeEvidenceArtifact artifact) => new
    {
        evidenceId = artifact.EvidenceId, kind = artifact.Kind, title = artifact.Title, content = artifact.Content,
        sha256 = artifact.Sha256, recordedByActorId = artifact.RecordedByActorId, recordedAtUtc = artifact.RecordedAtUtc
    };
    private static DateTimeOffset Now()
    {
        var time = TimeProvider.System.GetUtcNow();
        return new(time.Ticks - time.Ticks % 10, TimeSpan.Zero);
    }
    private static string Text(JsonElement root, string key)
    {
        try
        {
            return root.GetProperty(key).ValueKind == JsonValueKind.String
                ? root.GetProperty(key).GetString()! : throw new ArgumentException("Invalid governance request.");
        }
        catch (InvalidOperationException) { throw new ArgumentException("Invalid governance text encoding."); }
    }
    private static async Task<JsonDocument?> Body(HttpRequest request, string[] keys, CancellationToken token, int maximumBytes = 4096)
    {
        if (!request.HasJsonContentType() || request.ContentLength > maximumBytes) return null;
        try
        {
            using var stream = new MemoryStream();
            var buffer = new byte[1024];
            int read;
            while ((read = await request.Body.ReadAsync(buffer, token)) > 0)
            {
                if (stream.Length + read > maximumBytes) return null;
                stream.Write(buffer, 0, read);
            }
            var json = JsonDocument.Parse(new UTF8Encoding(false, true).GetString(stream.ToArray()));
            if (json.RootElement.ValueKind == JsonValueKind.Object
                && json.RootElement.EnumerateObject().Select(item => item.Name).Order(StringComparer.Ordinal).SequenceEqual(keys)) return json;
            json.Dispose();
        }
        catch (Exception exception) when (exception is JsonException or DecoderFallbackException) { }
        return null;
    }
}
