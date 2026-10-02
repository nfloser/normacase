import { type FormEvent, useEffect, useMemo, useRef, useState } from "react";
import { buildCaseJson, CaseInputError, emptyEvidence, emptyValues } from "./caseBuilder";
import { de, outcomeLabel } from "./i18n/de";
import type { AssessmentDocument, EvidenceInput, ExampleForm, PackCatalogItem } from "./types";

interface ResultState {
  raw: string;
  parsed: AssessmentDocument;
}

const truthOptions = [
  ["UNKNOWN", de.unknown],
  ["YES", de.yes],
  ["NO", de.no],
  ["NOT_APPLICABLE", de.notApplicable]
] as const;

const evidenceOptions: ReadonlyArray<readonly [EvidenceInput, string]> = [
  ["MISSING", de.evidenceMissing],
  ["PRESENT", de.evidencePresent],
  ["CONFLICTING", de.evidenceConflicting]
];

export default function App() {
  const [packs, setPacks] = useState<PackCatalogItem[]>([]);
  const [packId, setPackId] = useState("");
  const [assessmentDate, setAssessmentDate] = useState("");
  const [values, setValues] = useState<Record<string, string>>({});
  const [evidence, setEvidence] = useState<Record<string, EvidenceInput>>({});
  const [exampleId, setExampleId] = useState("");
  const [result, setResult] = useState<ResultState | null>(null);
  const [showJson, setShowJson] = useState(false);
  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [message, setMessage] = useState<string | null>(null);
  const requestRef = useRef<AbortController | null>(null);
  const generationRef = useRef(0);
  const resultHeadingRef = useRef<HTMLHeadingElement>(null);

  const pack = useMemo(() => packs.find((item) => item.packId === packId) ?? null, [packs, packId]);
  const outputPresentations = useMemo(
    () => new Map(pack?.presentation.outputs.map((item) => [item.id, item]) ?? []),
    [pack]
  );
  const fieldLabels = useMemo(
    () => new Map(pack?.fields.map((item) => [item.id, item.label]) ?? []),
    [pack]
  );

  function invalidateResult() {
    generationRef.current += 1;
    requestRef.current?.abort();
    requestRef.current = null;
    setSubmitting(false);
    setResult(null);
    setShowJson(false);
    setMessage(null);
  }

  function selectPack(next: PackCatalogItem) {
    invalidateResult();
    setPackId(next.packId);
    setAssessmentDate("");
    setValues(emptyValues(next));
    setEvidence(emptyEvidence(next));
    setExampleId("");
  }

  useEffect(() => {
    const controller = new AbortController();
    fetch("/api/packs", { signal: controller.signal, headers: { Accept: "application/json" } })
      .then(async (response) => {
        if (!response.ok) throw new Error("catalog");
        return await response.json() as PackCatalogItem[];
      })
      .then((items) => {
        setPacks(items);
        if (items.length > 0) {
          const first = items[0];
          setPackId(first.packId);
          setValues(emptyValues(first));
          setEvidence(emptyEvidence(first));
        }
      })
      .catch((error: unknown) => {
        if (!(error instanceof DOMException && error.name === "AbortError")) setMessage(de.loadError);
      })
      .finally(() => setLoading(false));
    return () => controller.abort();
  }, []);

  async function loadExample(nextExampleId: string) {
    if (!pack) return;
    invalidateResult();
    setExampleId(nextExampleId);
    if (!nextExampleId) {
      setAssessmentDate("");
      setValues(emptyValues(pack));
      setEvidence(emptyEvidence(pack));
      return;
    }

    try {
      const response = await fetch(`/api/packs/${encodeURIComponent(pack.packId)}/examples/${encodeURIComponent(nextExampleId)}`, {
        headers: { Accept: "application/json" }
      });
      if (!response.ok) throw new Error("example");
      const form = await response.json() as ExampleForm;
      setAssessmentDate(form.assessmentDate);
      setValues(form.values);
      setEvidence(form.evidence);
    } catch {
      setMessage(de.exampleError);
    }
  }

  function setField(id: string, value: string) {
    invalidateResult();
    setValues((current) => ({ ...current, [id]: value }));
  }

  function setEvidenceValue(id: string, value: EvidenceInput) {
    invalidateResult();
    setEvidence((current) => ({ ...current, [id]: value }));
  }

  function setDate(value: string) {
    invalidateResult();
    setAssessmentDate(value);
  }

  function reset() {
    if (!pack) return;
    invalidateResult();
    setAssessmentDate("");
    setValues(emptyValues(pack));
    setEvidence(emptyEvidence(pack));
    setExampleId("");
  }

  async function submit(event: FormEvent) {
    event.preventDefault();
    if (!pack) return;

    let body: string;
    try {
      body = buildCaseJson(pack, assessmentDate, values, evidence);
    } catch (error) {
      if (error instanceof CaseInputError) {
        setMessage(error.fieldId === "assessmentDate" ? de.dateRequired : de.invalidNumber);
        document.getElementById(error.fieldId)?.focus();
        return;
      }
      throw error;
    }

    invalidateResult();
    const generation = generationRef.current;
    const controller = new AbortController();
    requestRef.current = controller;
    setSubmitting(true);

    try {
      const response = await fetch(`/api/assessments/${encodeURIComponent(pack.packId)}`, {
        method: "POST",
        headers: { "Content-Type": "application/json", Accept: "application/json" },
        body,
        signal: controller.signal
      });
      const raw = await response.text();
      if (!response.ok) throw new Error("assessment");
      const parsed = JSON.parse(raw) as AssessmentDocument;
      if (generation !== generationRef.current) return;
      setResult({ raw, parsed });
      queueMicrotask(() => resultHeadingRef.current?.focus());
    } catch (error: unknown) {
      if (!(error instanceof DOMException && error.name === "AbortError") && generation === generationRef.current) {
        setMessage(de.assessmentError);
      }
    } finally {
      if (generation === generationRef.current) {
        setSubmitting(false);
        requestRef.current = null;
      }
    }
  }

  function exportJson() {
    if (!result) return;
    const blob = new Blob([result.raw], { type: "application/json;charset=utf-8" });
    const url = URL.createObjectURL(blob);
    const link = document.createElement("a");
    link.href = url;
    link.download = `normacase-assessment-${result.parsed.assessment.assessmentDate}.json`;
    link.click();
    URL.revokeObjectURL(url);
  }

  const assessment = result?.parsed.assessment;
  const source = assessment?.ruleTrace?.source;

  return (
    <>
      <a className="skip-link" href="#assessment-form">{de.skipToForm}</a>
      <header className="hero">
        <div className="hero__inner">
          <p className="eyebrow">{de.appTitle}</p>
          <h1>{de.appSubtitle}</h1>
          <p className="hero__copy">Deterministische, nachvollziehbare Tests mit synthetischen Wissensständen.</p>
          <div className="notice" role="note">{de.syntheticNotice}</div>
        </div>
      </header>

      <main className="layout">
        <section className="panel panel--form" aria-labelledby="form-heading">
          <div className="section-heading">
            <p className="step">01</p>
            <div>
              <h2 id="form-heading">Prüfung konfigurieren</h2>
              <p>Wähle einen synthetischen Wissensstand und erfasse die Angaben explizit.</p>
            </div>
          </div>

          {loading ? <p role="status">Prüfdaten werden geladen …</p> : null}
          {message ? <div className="error" role="alert">{message}</div> : null}

          <form id="assessment-form" onSubmit={submit}>
            <div className="control-grid">
              <label className="control">
                <span>{de.pack}</span>
                <select
                  value={packId}
                  onChange={(event) => {
                    const next = packs.find((item) => item.packId === event.target.value);
                    if (next) selectPack(next);
                  }}
                  disabled={packs.length === 0}
                >
                  {packs.map((item) => <option key={item.packId} value={item.packId}>{item.presentation.name}</option>)}
                </select>
              </label>

              <label className="control">
                <span>{de.example}</span>
                <select value={exampleId} onChange={(event) => void loadExample(event.target.value)} disabled={!pack}>
                  <option value="">{de.noExample}</option>
                  {pack?.presentation.examples.map((item) => <option key={item.id} value={item.id}>{item.label}</option>)}
                </select>
              </label>

              <label className="control">
                <span>{de.assessmentDate}</span>
                <input id="assessmentDate" type="date" value={assessmentDate} onChange={(event) => setDate(event.target.value)} required />
              </label>
            </div>

            {pack ? <p className="pack-description">{pack.presentation.description}</p> : null}

            {pack && pack.fields.length > 0 ? (
              <fieldset>
                <legend>Falldaten</legend>
                <div className="field-list">
                  {pack.fields.map((field) => (
                    <label className="field-card" key={field.id} htmlFor={field.id}>
                      <span className="field-card__title">
                        {field.label}
                        <span className="badge">{field.required ? de.required : de.optional}</span>
                      </span>
                      {field.helpText ? <span className="help">{field.helpText}</span> : null}
                      {field.type === "truth" ? (
                        <select id={field.id} value={values[field.id] ?? "UNKNOWN"} onChange={(event) => setField(field.id, event.target.value)}>
                          {truthOptions.map(([value, label]) => <option key={value} value={value}>{label}</option>)}
                        </select>
                      ) : (
                        <input
                          id={field.id}
                          type="text"
                          inputMode="decimal"
                          autoComplete="off"
                          value={values[field.id] ?? ""}
                          onChange={(event) => setField(field.id, event.target.value)}
                          placeholder={de.unknown}
                        />
                      )}
                    </label>
                  ))}
                </div>
              </fieldset>
            ) : null}

            {pack && pack.evidenceRequirements.length > 0 ? (
              <fieldset>
                <legend>Evidenzstatus</legend>
                <div className="field-list">
                  {pack.evidenceRequirements.map((item) => (
                    <label className="field-card" key={item.id} htmlFor={`evidence-${item.id}`}>
                      <span className="field-card__title">{item.label}</span>
                      {item.helpText ? <span className="help">{item.helpText}</span> : null}
                      <select
                        id={`evidence-${item.id}`}
                        value={evidence[item.id] ?? "MISSING"}
                        onChange={(event) => setEvidenceValue(item.id, event.target.value as EvidenceInput)}
                      >
                        {evidenceOptions.map(([value, label]) => <option key={value} value={value}>{label}</option>)}
                      </select>
                    </label>
                  ))}
                </div>
              </fieldset>
            ) : null}

            <div className="actions">
              <button className="button button--primary" type="submit" disabled={!pack || submitting}>
                {submitting ? de.evaluating : de.evaluate}
              </button>
              <button className="button button--secondary" type="button" onClick={reset} disabled={!pack || submitting}>{de.reset}</button>
            </div>
          </form>
        </section>

        <section className="panel panel--result" aria-labelledby="result-heading" aria-live="polite">
          <div className="section-heading">
            <p className="step">02</p>
            <div>
              <h2 id="result-heading" tabIndex={-1} ref={resultHeadingRef}>{de.result}</h2>
              <p>Plattformstatus und fachliche Ausgaben bleiben voneinander getrennt.</p>
            </div>
          </div>

          {!assessment ? <div className="empty-state">{de.noResult}</div> : (
            <div className="result-stack">
              <div className={`outcome outcome--${assessment.outcome.toLowerCase().replaceAll("_", "-")}`}>
                <span>Plattformstatus</span>
                <strong>{outcomeLabel(assessment.outcome)}</strong>
              </div>

              <dl className="facts">
                <div><dt>{de.assessmentDate}</dt><dd>{formatGermanDate(assessment.assessmentDate)}</dd></div>
                <div><dt>{de.knowledgeRelease}</dt><dd><code>{assessment.knowledgeRelease}</code></dd></div>
                <div><dt>{de.platformVersion}</dt><dd><code>{result!.parsed.platformVersion}</code></dd></div>
                {assessment.ruleTrace ? <div><dt>{de.rule}</dt><dd><code>{assessment.ruleTrace.ruleId}@{assessment.ruleTrace.ruleVersion}</code></dd></div> : null}
              </dl>

              <section className="result-section">
                <h3>{de.completeness}</h3>
                {assessment.missingRequiredFields.length === 0
                  ? <p>{de.complete}</p>
                  : <ul>{assessment.missingRequiredFields.map((id) => <li key={id}>{fieldLabels.get(id) ?? id}</li>)}</ul>}
              </section>

              {source ? (
                <section className="result-section">
                  <h3>{de.source}</h3>
                  <p><strong>{source.title}</strong></p>
                  <p className="muted">{source.version ? `${de.sourceVersion}: ${source.version}` : ""}{source.sourceLocation ? ` · ${source.sourceLocation}` : ""}</p>
                </section>
              ) : null}

              {assessment.domainOutputs.length > 0 ? (
                <section className="result-section">
                  <h3>{de.domainOutputs}</h3>
                  <div className="output-list">
                    {assessment.domainOutputs.map((item) => (
                      <article className="output-card" key={item.outputId}>
                        <span>{outputPresentations.get(item.outputId)?.label ?? item.outputId}</span>
                        <strong>{item.value.kind === "UNKNOWN"
                          ? "Nicht ausreichend beurteilbar"
                          : outputPresentations.get(item.outputId)?.choices[item.value.choice ?? ""] ?? item.value.choice}</strong>
                        <small>{item.source.title}{item.source.version ? ` · ${item.source.version}` : ""}</small>
                      </article>
                    ))}
                  </div>
                </section>
              ) : null}

              <section className="result-section">
                <div className="json-heading">
                  <h3>{de.exactJson}</h3>
                  <div className="actions actions--compact">
                    <button className="button button--secondary" type="button" onClick={() => setShowJson((value) => !value)}>{showJson ? de.hideJson : de.showJson}</button>
                    <button className="button button--secondary" type="button" onClick={exportJson}>{de.exportJson}</button>
                  </div>
                </div>
                {showJson ? <pre className="json-output" data-testid="raw-json">{result!.raw}</pre> : null}
              </section>
            </div>
          )}
        </section>
      </main>
    </>
  );
}

function formatGermanDate(value: string): string {
  const match = /^(\d{4})-(\d{2})-(\d{2})$/.exec(value);
  return match ? `${match[3]}.${match[2]}.${match[1]}` : value;
}
