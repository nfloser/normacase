export type FieldType = "truth" | "number";
export type TruthInput = "UNKNOWN" | "YES" | "NO" | "NOT_APPLICABLE";
export type EvidenceInput = "MISSING" | "PRESENT" | "CONFLICTING";

export interface PackField {
  id: string;
  type: FieldType;
  required: boolean;
  label: string;
  helpText: string | null;
}

export interface EvidenceRequirement {
  id: string;
  label: string;
  helpText: string | null;
}

export interface OutputPresentation {
  id: string;
  label: string;
  choices: Record<string, string>;
}

export interface PackExample {
  id: string;
  label: string;
}

export interface PackCatalogItem {
  packId: string;
  releaseId: string;
  validationLevel: "SYNTHETIC";
  presentation: {
    locale: "de-DE";
    name: string;
    description: string;
    outputs: OutputPresentation[];
    examples: PackExample[];
  };
  fields: PackField[];
  evidenceRequirements: EvidenceRequirement[];
}

export interface ExampleForm {
  assessmentDate: string;
  values: Record<string, string>;
  evidence: Record<string, EvidenceInput>;
}

export interface DomainOutput {
  outputId: string;
  outputVersion: number;
  value: { kind: "UNKNOWN" | "CHOICE"; choice?: string | null };
  source: {
    id: string;
    title: string;
    version?: string | null;
    sourceLocation?: string | null;
  };
}

export interface AssessmentDocument {
  formatVersion: number;
  platformVersion: string;
  assessment: {
    knowledgeRelease: string;
    assessmentDate: string;
    outcome: "SUPPORTED" | "NOT_SUPPORTED" | "INCOMPLETE" | "HUMAN_REVIEW" | "NOT_APPLICABLE";
    missingRequiredFields: string[];
    ruleTrace: null | {
      ruleId: string;
      ruleVersion: number;
      sourceId: string;
      source: {
        id: string;
        title: string;
        version?: string | null;
        sourceLocation?: string | null;
      };
    };
    domainOutputs: DomainOutput[];
  };
}
