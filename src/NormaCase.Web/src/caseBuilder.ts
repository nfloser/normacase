import type { EvidenceInput, PackCatalogItem, TruthInput } from "./types";

const decimalPattern = /^-?(?:0|[1-9]\d*)(?:\.\d+)?$/;

export class CaseInputError extends Error {
  constructor(public readonly fieldId: string) {
    super(fieldId);
  }
}

export function buildCaseJson(
  pack: PackCatalogItem,
  assessmentDate: string,
  values: Record<string, string>,
  evidence: Record<string, EvidenceInput>
): string {
  if (!/^\d{4}-\d{2}-\d{2}$/.test(assessmentDate)) {
    throw new CaseInputError("assessmentDate");
  }

  const facts = pack.fields.map((field) => {
    const raw = values[field.id] ?? "UNKNOWN";
    let valueJson: string;

    if (field.type === "truth") {
      const truth = raw as TruthInput;
      valueJson = truth === "UNKNOWN"
        ? '{"kind":"UNKNOWN"}'
        : `{"kind":"TRUTH","truth":${JSON.stringify(truth)}}`;
    } else if (raw === "" || raw === "UNKNOWN") {
      valueJson = '{"kind":"UNKNOWN"}';
    } else {
      if (!decimalPattern.test(raw)) {
        throw new CaseInputError(field.id);
      }
      valueJson = `{"kind":"NUMBER","number":${raw}}`;
    }

    return `${JSON.stringify(field.id)}:${valueJson}`;
  });

  const evidenceJson = pack.evidenceRequirements
    .map((item) => `${JSON.stringify(item.id)}:${JSON.stringify(evidence[item.id] ?? "MISSING")}`)
    .join(",");

  return `{"formatVersion":1,"assessmentDate":${JSON.stringify(assessmentDate)},"facts":{${facts.join(",")}},"evidence":{${evidenceJson}}}`;
}

export function emptyValues(pack: PackCatalogItem): Record<string, string> {
  return Object.fromEntries(
    pack.fields.map((field) => [field.id, field.type === "truth" ? "UNKNOWN" : ""])
  );
}

export function emptyEvidence(pack: PackCatalogItem): Record<string, EvidenceInput> {
  return Object.fromEntries(pack.evidenceRequirements.map((item) => [item.id, "MISSING"]));
}
