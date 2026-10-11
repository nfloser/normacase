using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using NormaCase.Domain.Cases;

namespace NormaCase.Application.Retention;

public enum RetentionStore { CaseDatabase, Documents, OutboundExports, Audit, Backups }
public enum RetentionHold { Unknown, Active, Released }
public enum RetentionDisposition { HumanReview, Retain, RetainLegalHold, EligibleForReview }
public enum RetentionReason { InventoryIncomplete, UnknownHold, ActiveHold, UnknownExpiry, RetentionActive, Expired }

/// <summary>Metadata supplied by a trusted inventory adapter, never patient content.</summary>
public sealed record RetentionArtifact(string ArtifactId, RetentionStore Store,
    DateTimeOffset? RetainUntilUtc, RetentionHold Hold);

public sealed record RetentionArtifactPlan(string ArtifactId, RetentionStore Store,
    DateTimeOffset? RetainUntilUtc, RetentionHold Hold,
    RetentionDisposition Disposition, RetentionReason Reason);

/// <summary>A detached planning snapshot. It cannot authorize or execute erasure.</summary>
public sealed class RetentionPlan
{
    internal RetentionPlan(CaseId caseId, string policyId, string policyVersion,
        DateTimeOffset evaluatedAtUtc, RetentionArtifactPlan[] artifacts)
    {
        CaseId = caseId;
        PolicyId = policyId;
        PolicyVersion = policyVersion;
        EvaluatedAtUtc = evaluatedAtUtc;
        Artifacts = Array.AsReadOnly(artifacts);
    }

    public CaseId CaseId { get; }
    public string PolicyId { get; }
    public string PolicyVersion { get; }
    public DateTimeOffset EvaluatedAtUtc { get; }
    public ReadOnlyCollection<RetentionArtifactPlan> Artifacts { get; }
    public bool AllArtifactsEligibleForReview => Artifacts.All(x => x.Disposition == RetentionDisposition.EligibleForReview);
    public bool DeletionAuthorized => false;
}

/// <summary>Read-only housekeeping planning, separate from medical rule evaluation.</summary>
public sealed class RetentionPlanner
{
    public const int MaximumArtifacts = 1024;
    private static readonly Regex Identifier = new("\\A[A-Za-z0-9][A-Za-z0-9._-]*\\z", RegexOptions.CultureInvariant);

    public RetentionPlan Plan(CaseId caseId, string policyId, string policyVersion,
        DateTimeOffset evaluatedAtUtc, bool inventoryComplete, IEnumerable<RetentionArtifact> artifacts)
    {
        if (caseId.IsEmpty) throw new ArgumentException("Explicit case identity required.", nameof(caseId));
        ValidateIdentity(policyId);
        ValidateIdentity(policyVersion);
        ValidateUtc(evaluatedAtUtc);
        ArgumentNullException.ThrowIfNull(artifacts);
        var snapshot = artifacts.Take(MaximumArtifacts + 1).ToArray();
        if (snapshot.Length is 0 or > MaximumArtifacts)
            throw new ArgumentException("Invalid bounded retention inventory.", nameof(artifacts));
        var identities = new HashSet<string>(StringComparer.Ordinal);
        foreach (var artifact in snapshot)
        {
            if (artifact is null) throw new ArgumentException("Missing artifact metadata.", nameof(artifacts));
            ValidateIdentity(artifact.ArtifactId);
            if (!identities.Add(artifact.ArtifactId) || !Enum.IsDefined(artifact.Store) || !Enum.IsDefined(artifact.Hold))
                throw new ArgumentException("Invalid artifact identity or state.", nameof(artifacts));
            if (artifact.RetainUntilUtc is { } until) ValidateUtc(until);
        }

        var plans = snapshot.OrderBy(x => x.ArtifactId, StringComparer.Ordinal).Select(artifact =>
        {
            var (disposition, reason) = Classify(artifact, evaluatedAtUtc, inventoryComplete);
            return new RetentionArtifactPlan(artifact.ArtifactId, artifact.Store,
                artifact.RetainUntilUtc, artifact.Hold, disposition, reason);
        }).ToArray();
        return new(caseId, policyId, policyVersion, evaluatedAtUtc, plans);
    }

    private static (RetentionDisposition, RetentionReason) Classify(RetentionArtifact artifact,
        DateTimeOffset now, bool complete)
    {
        if (artifact.Hold == RetentionHold.Active) return (RetentionDisposition.RetainLegalHold, RetentionReason.ActiveHold);
        if (!complete) return (RetentionDisposition.HumanReview, RetentionReason.InventoryIncomplete);
        if (artifact.Hold == RetentionHold.Unknown) return (RetentionDisposition.HumanReview, RetentionReason.UnknownHold);
        if (artifact.RetainUntilUtc is null) return (RetentionDisposition.HumanReview, RetentionReason.UnknownExpiry);
        return now < artifact.RetainUntilUtc.Value
            ? (RetentionDisposition.Retain, RetentionReason.RetentionActive)
            : (RetentionDisposition.EligibleForReview, RetentionReason.Expired);
    }

    private static void ValidateIdentity(string value)
    {
        if (value is null || value.Length > 128 || !Identifier.IsMatch(value))
            throw new ArgumentException("Invalid bounded retention identity.");
    }

    private static void ValidateUtc(DateTimeOffset value)
    {
        if (value == default || value.Offset != TimeSpan.Zero)
            throw new ArgumentException("Explicit UTC timestamp required.");
    }
}
