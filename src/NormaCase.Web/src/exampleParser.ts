import type { EvidenceInput, ExampleForm, PackCatalogItem } from "./types";

const numericToken = /("number"\s*:\s*)(-?(?:0|[1-9]\d*)(?:\.\d+)?)(?=\s*[,}])/g;

interface ParsedCaseValue {
  kind: "UNKNOWN" | "TRUTH" | "NUMBER";
  truth?: string;
  number?: string;
}

interface ParsedCaseInput {
  formatVersion: number;
  assessmentDate: string;
  facts: Record<string, ParsedCaseValue>;
  evidence?: Record<string, EvidenceInput>;
}

export function parseExampleForm(pack: PackCatalogItem, rawJson: string): ExampleForm {
  const lossless = rawJson.replace(numericToken, '$1"$2"');
  const parsed = JSON.parse(lossless) as ParsedCaseInput;

  if (parsed.formatVersion !== 1 || !/^\d{4}-\d{2}-\d{2}$/.test(parsed.assessmentDate)) {
    throw new Error("invalid example");
  }

  const values: Record<string, string> = {};
  for (const field of pack.fields) {
    const value = parsed.facts[field.id];
    if (!value || value.kind === "UNKNOWN") {
      values[field.id] = field.type === "truth" ? "UNKNOWN" : "";
      continue;
    }

    if (field.type === "truth" && value.kind === "TRUTH"
        && ["YES", "NO", "NOT_APPLICABLE"].includes(value.truth ?? "")) {
      values[field.id] = value.truth!;
      continue;
    }

    if (field.type === "number" && value.kind === "NUMBER" && typeof value.number === "string") {
      values[field.id] = value.number;
      continue;
    }

    throw new Error("example type mismatch");
  }

  const evidence = Object.fromEntries(
    pack.evidenceRequirements.map((item) => {
      const value = parsed.evidence?.[item.id] ?? "MISSING";
      if (!["MISSING", "PRESENT", "CONFLICTING"].includes(value)) {
        throw new Error("invalid example evidence");
      }
      return [item.id, value];
    })
  ) as Record<string, EvidenceInput>;

  return { assessmentDate: parsed.assessmentDate, values, evidence };
}
