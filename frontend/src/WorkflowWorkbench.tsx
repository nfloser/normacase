import React, { useEffect, useRef, useState } from 'react';
import { LosslessNumber, stringify } from 'lossless-json';
import de from './de.json';
import type { Pack } from './model';

interface WorkflowEnvelope {
  runJson: string;
  view: {
    runId: string; caseId: string; platformVersion: string; knowledgePackId: string; knowledgeRelease: string;
    workflowId: string; workflowVersion: number; workflowLabel: string; revision: string; stateLabel: string; terminal: boolean;
    transitions: { id: string; label: string }[];
    history: { revision: string; stateLabel: string; transitionLabel: string; actorId: string; recordedAtUtc: string; reason: string }[];
  };
}

export function WorkflowWorkbench({ pack }: { pack: Pack }) {
  const labels = de.workflow;
  const workflows = pack.presentation?.workflows ?? [];
  const [workflowKey, setWorkflowKey] = useState(workflows[0] ? workflows[0].id + '@' + workflows[0].version : '');
  const [caseId, setCaseId] = useState('');
  const [runId, setRunId] = useState('');
  const [actor, setActor] = useState('');
  const [time, setTime] = useState('');
  const [reason, setReason] = useState('');
  const [transition, setTransition] = useState('');
  const [envelope, setEnvelope] = useState<WorkflowEnvelope | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [verified, setVerified] = useState(false);
  const pending = useRef<AbortController | null>(null);
  const fileInput = useRef<HTMLInputElement | null>(null);
  useEffect(() => () => pending.current?.abort(), []);
  if (!workflows.length) return null;

  function invalidate(clearRun = false) {
    pending.current?.abort(); setBusy(false); setError(''); setVerified(false);
    if (clearRun) { setEnvelope(null); setTransition(''); }
  }

  function install(next: WorkflowEnvelope) {
    if (typeof next.runJson !== 'string' || next.view.knowledgePackId !== pack.packId)
      throw new Error();
    setEnvelope(next); setTransition(next.view.transitions[0]?.id ?? '');
    setCaseId(next.view.caseId); setRunId(next.view.runId);
    setWorkflowKey(next.view.workflowId + '@' + next.view.workflowVersion);
  }

  async function send(path: string, body: string, controller: AbortController) {
    const response = await fetch(path, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body, signal: controller.signal });
    const text = await response.text();
    if (controller.signal.aborted) return;
    if (!response.ok) { setError((JSON.parse(text) as { message?: string }).message ?? de.inputError); return; }
    const next = JSON.parse(text) as WorkflowEnvelope;
    if (next.view.knowledgePackId !== pack.packId) { setError(de.inputError); return; }
    install(next);
    return true;
  }

  async function submit(event: React.FormEvent) {
    event.preventDefault(); invalidate();
    const controller = new AbortController(); pending.current = controller; setBusy(true);
    try {
      if (envelope) {
        await send('/api/workflows/advance', stringify({ runJson: envelope.runJson,
          expectedRevision: new LosslessNumber(envelope.view.revision), transitionId: transition,
          actorId: actor, recordedAtUtc: time, reason })!, controller);
      } else {
        const workflow = workflows.find(item => item.id + '@' + item.version === workflowKey);
        if (!workflow) throw new Error();
        await send('/api/workflows/' + encodeURIComponent(pack.packId) + '/start', JSON.stringify({
          workflowId: workflow.id, workflowVersion: workflow.version, runId, caseId,
          actorId: actor, recordedAtUtc: time, reason }), controller);
      }
    } catch { if (!controller.signal.aborted) setError(de.networkError); }
    finally { if (!controller.signal.aborted) setBusy(false); }
  }

  async function loadFile(event: React.ChangeEvent<HTMLInputElement>) {
    const file = event.currentTarget.files?.[0]; event.currentTarget.value = '';
    if (!file) return;
    invalidate(true);
    if (file.size > 1024 * 1024) { setError(de.snapshotTooLarge); return; }
    const controller = new AbortController(); pending.current = controller; setBusy(true);
    try {
      const bytes = await file.arrayBuffer();
      if (controller.signal.aborted) return;
      let body: string;
      try { body = new TextDecoder('utf-8', { fatal: true }).decode(bytes); }
      catch { setError(de.snapshotEncodingError); return; }
      const accepted = await send('/api/workflows/verify', body, controller);
      if (accepted && !controller.signal.aborted) setVerified(true);
    } catch { if (!controller.signal.aborted) setError(de.networkError); }
    finally { if (!controller.signal.aborted) setBusy(false); }
  }

  function download() {
    if (!envelope) return;
    const url = URL.createObjectURL(new Blob([envelope.runJson], { type: 'application/json' }));
    const link = document.createElement('a'); link.href = url; link.download = 'normacase-vorgang.json'; link.click();
    setTimeout(() => URL.revokeObjectURL(url), 1000);
  }

  return <section className="card workflow-tools" aria-labelledby="workflow-heading">
    <h2 id="workflow-heading">{labels.heading}</h2><p>{labels.help}</p><p className="notice">{labels.notice}</p>
    <form onSubmit={submit}>
      <label className="field">{labels.select}<select value={workflowKey} disabled={!!envelope} onChange={event => { invalidate(true); setWorkflowKey(event.target.value); }}>
        {workflows.map(item => <option key={item.id + '@' + item.version} value={item.id + '@' + item.version}>{item.label}</option>)}
      </select></label>
      <div className="fields">
        <label className="field">{labels.caseId}<input required value={caseId} disabled={!!envelope} onChange={event => { invalidate(); setCaseId(event.target.value); }} /></label>
        <label className="field">{labels.runId}<input required value={runId} disabled={!!envelope} onChange={event => { invalidate(); setRunId(event.target.value); }} /></label>
        <label className="field">{labels.actor}<input required value={actor} onChange={event => { invalidate(); setActor(event.target.value); }} /></label>
        <label className="field">{labels.time}<input required type="text" value={time} placeholder="2026-10-03T12:00:00Z" onChange={event => { invalidate(); setTime(event.target.value); }} /></label>
      </div><p className="workflow-help">{labels.timeHelp}</p>
      <label className="field">{labels.reason}<textarea required value={reason} onChange={event => { invalidate(); setReason(event.target.value); }} /></label>
      {envelope && !envelope.view.terminal && <label className="field">{labels.transition}<select value={transition} onChange={event => { invalidate(); setTransition(event.target.value); }}>
        {envelope.view.transitions.map(item => <option key={item.id} value={item.id}>{item.label}</option>)}
      </select></label>}
      {error && <div className="error" role="alert"><strong>{de.errorHeading}</strong><p>{error}</p></div>}
      <div className="actions">
        {!envelope?.view.terminal && <button className="primary" disabled={busy}>{busy ? labels.working : envelope ? labels.apply : labels.start}</button>}
        {!envelope && <button type="button" className="secondary" disabled={busy} onClick={() => {
          invalidate(true); setCaseId('case-synthetic-workbench'); setRunId('run-synthetic-workbench');
          setActor('synthetic-reviewer'); setTime('2026-10-03T12:00:00Z'); setReason('Synthetischer Testschritt');
        }}>{labels.loadDemo}</button>}
        <button type="button" className="text-button" onClick={() => { invalidate(true); setCaseId(''); setRunId(''); setActor(''); setTime(''); setReason(''); }}>{labels.reset}</button>
      </div>
    </form>
    <div aria-live="polite">
      {verified && <h3>{labels.verified}</h3>}
      {envelope && <><h3>{labels.state}: {envelope.view.stateLabel}</h3>
        <dl><dt>{labels.revision}</dt><dd>{envelope.view.revision}</dd><dt>{de.release}</dt><dd>{envelope.view.knowledgeRelease}</dd><dt>{de.platform}</dt><dd>{envelope.view.platformVersion}</dd></dl>
        {envelope.view.terminal && <p>{labels.terminal}</p>}
        <h3>{labels.history}</h3><ol className="workflow-history">{envelope.view.history.map(item => <li key={item.revision}>
          <h4>{item.stateLabel} · {labels.revision} {item.revision}</h4><p>{item.transitionLabel}</p>
          <dl><dt>{labels.actor}</dt><dd>{item.actorId}</dd><dt>{labels.time}</dt><dd>{new Date(item.recordedAtUtc).toLocaleString('de-DE', { timeZone: 'UTC' })} UTC</dd><dt>{labels.reason}</dt><dd>{item.reason}</dd></dl>
        </li>)}</ol><button type="button" className="secondary export" onClick={download}>{labels.export}</button>
      </>}
    </div>
    <p>{labels.importHelp}</p><input type="file" hidden ref={fileInput} aria-label={labels.file} accept=".json,application/json" disabled={busy} onChange={loadFile} />
    <button type="button" className="secondary file-button" disabled={busy} onClick={() => fileInput.current?.click()}>{labels.import}</button>
  </section>;
}
