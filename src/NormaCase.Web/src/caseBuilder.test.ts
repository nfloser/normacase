import { describe, expect, it } from "vitest";
import { buildCaseJson, CaseInputError } from "./caseBuilder";
import type { PackCatalogItem } from "./types";

const pack: PackCatalogItem = {
  packId: "synthetic.precision",
  releaseId: "1",
  validationLevel: "SYNTHETIC",
  presentation: {
    locale: "de-DE",
    name: "Präzision",
    description: "Test",
    outputs: [],
    examples: []
  },
  fields: [
    { id: "exact", type: "number", required: true, label: "Exakter Wert", helpText: null },
    { id: "flag", type: "truth", required: false, label: "Flag", helpText: null }
  ],
  evidenceRequirements: [
    { id: "proof", label: "Nachweis", helpText: null }
  ]
};

describe("buildCaseJson", () => {
  it("preserves decimal text without JavaScript Number conversion", () => {
    const decimal = "123456789.1234567890123456789";
    const json = buildCaseJson(
      pack,
      "2026-10-02",
      { exact: decimal, flag: "UNKNOWN" },
      { proof: "PRESENT" }
    );

    expect(json).toContain(`"number":${decimal}`);
    expect(json).toContain('"flag":{"kind":"UNKNOWN"}');
    expect(json).toContain('"proof":"PRESENT"');
  });

  it("rejects locale-formatted or exponent values before submission", () => {
    for (const value of ["1,5", "1e3", "+1", "01"]) {
      expect(() => buildCaseJson(pack, "2026-10-02", { exact: value, flag: "NO" }, {}))
        .toThrow(CaseInputError);
    }
  });

  it("requires an explicit ISO assessment date", () => {
    expect(() => buildCaseJson(pack, "", { exact: "1", flag: "YES" }, {}))
      .toThrow(CaseInputError);
  });
});
