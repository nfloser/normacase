import { describe, expect, it } from "vitest";
import { parseExampleForm } from "./exampleParser";
import type { PackCatalogItem } from "./types";

const pack: PackCatalogItem = {
  packId: "synthetic.precision",
  releaseId: "1",
  validationLevel: "SYNTHETIC",
  presentation: { locale: "de-DE", name: "Präzision", description: "Test", outputs: [], examples: [] },
  fields: [
    { id: "exact", type: "number", required: true, label: "Exakter Wert", helpText: null },
    { id: "flag", type: "truth", required: false, label: "Flag", helpText: null }
  ],
  evidenceRequirements: [{ id: "proof", label: "Nachweis", helpText: null }]
};

describe("parseExampleForm", () => {
  it("keeps the original decimal lexeme", () => {
    const decimal = "123456789.1234567890123456789";
    const form = parseExampleForm(pack, `{"formatVersion":1,"assessmentDate":"2026-10-02","facts":{"exact":{"kind":"NUMBER","number":${decimal}},"flag":{"kind":"UNKNOWN"}},"evidence":{"proof":"PRESENT"}}`);
    expect(form.values.exact).toBe(decimal);
    expect(form.values.flag).toBe("UNKNOWN");
    expect(form.evidence.proof).toBe("PRESENT");
  });
});
