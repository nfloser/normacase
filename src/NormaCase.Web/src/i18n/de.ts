export const de = {
  appTitle: "NormaCase",
  appSubtitle: "Synthetische Prüfwerkbank",
  syntheticNotice: "Nur synthetische Demonstration – keine fachlich freigegebene Begutachtung.",
  pack: "Synthetisches Regelpaket",
  choosePack: "Beispiel auswählen",
  assessmentDate: "Prüfdatum",
  example: "Beispielfall",
  noExample: "Ohne Vorlage starten",
  required: "Pflichtangabe",
  optional: "Optional",
  unknown: "Unbekannt",
  yes: "Ja",
  no: "Nein",
  notApplicable: "Nicht anwendbar",
  evidenceMissing: "Fehlt",
  evidencePresent: "Vorhanden",
  evidenceConflicting: "Widersprüchlich",
  evaluate: "Prüfung ausführen",
  evaluating: "Prüfung läuft …",
  result: "Ergebnis",
  noResult: "Noch keine Prüfung ausgeführt.",
  completeness: "Vollständigkeit",
  complete: "Pflichtangaben vollständig",
  missing: "Fehlende Pflichtangaben",
  source: "Quelle",
  sourceVersion: "Quellenstand",
  rule: "Regel",
  knowledgeRelease: "Wissensstand",
  platformVersion: "Plattformstand",
  domainOutputs: "Fachliche Ausgaben",
  exactJson: "Technisches Assessment-JSON",
  exportJson: "JSON exportieren",
  showJson: "JSON anzeigen",
  hideJson: "JSON ausblenden",
  loadError: "Die synthetischen Prüfdaten konnten nicht geladen werden.",
  exampleError: "Das synthetische Beispiel konnte nicht geladen werden.",
  assessmentError: "Die Prüfung konnte nicht ausgeführt werden. Prüfe die Eingaben.",
  dateRequired: "Bitte ein Prüfdatum angeben.",
  invalidNumber: "Bitte eine gültige Dezimalzahl eingeben.",
  supported: "Voraussetzungen erfüllt",
  notSupported: "Voraussetzungen nicht erfüllt",
  incomplete: "Angaben unvollständig",
  humanReview: "Manuelle Prüfung erforderlich",
  outcomeNotApplicable: "Nicht anwendbar",
  skipToForm: "Zum Prüfformular springen",
  reset: "Eingaben zurücksetzen",
  heroCopy: "Deterministische, nachvollziehbare Tests mit synthetischen Wissensständen.",
  configureHeading: "Prüfung konfigurieren",
  configureCopy: "Wähle einen synthetischen Wissensstand und erfasse die Angaben explizit.",
  loading: "Prüfdaten werden geladen …",
  caseData: "Falldaten",
  evidenceStatus: "Evidenzstatus",
  resultCopy: "Plattformstatus und fachliche Ausgaben bleiben voneinander getrennt.",
  platformStatus: "Plattformstatus",
  unknownOutput: "Nicht ausreichend beurteilbar",
  numberStatus: "Eingabestatus",
  numberUnknown: "Unbekannt",
  numberValue: "Wert eingeben"
} as const;

export function outcomeLabel(outcome: AssessmentDocument["assessment"]["outcome"]): string {
  switch (outcome) {
    case "SUPPORTED": return de.supported;
    case "NOT_SUPPORTED": return de.notSupported;
    case "INCOMPLETE": return de.incomplete;
    case "HUMAN_REVIEW": return de.humanReview;
    case "NOT_APPLICABLE": return de.outcomeNotApplicable;
  }
}

import type { AssessmentDocument } from "../types";
