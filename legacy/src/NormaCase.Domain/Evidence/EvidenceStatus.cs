namespace NormaCase.Domain.Evidence;

/// <summary>Structured evidence availability, supplied by the caller; no document interpretation.</summary>
public enum EvidenceStatus
{
    Missing = 0,
    Present = 1,
    Conflicting = 2
}
