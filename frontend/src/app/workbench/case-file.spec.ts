import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ProcessDetail } from './api';
import { CaseAction, CaseFile } from './case-file';
import { detailJson } from './fixtures';
import { isObject, parseExact } from '../shared/json';

function detail(overrides = {}): ProcessDetail {
  const text = detailJson('a', overrides);
  const exact = parseExact(text);
  const plain = JSON.parse(text) as ProcessDetail;
  return { ...plain, assessment: isObject(exact) ? exact['assessment'] : null, caseInput: isObject(exact) ? exact['caseInput'] : null };
}

describe('CaseFile', () => {
  function render(d: ProcessDetail, username = 'gutachterin.a') {
    TestBed.configureTestingModule({ imports: [CaseFile], providers: [provideHttpClient(), provideHttpClientTesting()] });
    const fixture = TestBed.createComponent(CaseFile);
    fixture.componentRef.setInput('detail', d);
    fixture.componentRef.setInput('username', username);
    fixture.componentRef.setInput('release', {
      releaseId: 'r', fields: [{ id: 'request_complete', type: 'truth', required: true }, { id: 'score', type: 'number', required: false }],
      evidenceRequirements: ['supporting_document'],
    });
    const actions: CaseAction[] = [];
    fixture.componentInstance.act.subscribe((a) => actions.push(a));
    fixture.detectChanges();
    return { fixture, el: fixture.nativeElement as HTMLElement, actions };
  }

  it('bearbeitet die persönliche Notiz im eigenen Tab', () => {
    const { fixture, el } = render(detail());
    fixture.componentRef.setInput('note', 'Beim Fachteam nachfragen');
    const changes: string[] = [];
    fixture.componentInstance.noteChange.subscribe((note) => changes.push(note));
    [...el.querySelectorAll<HTMLButtonElement>('[role=tab]')].find((tab) => tab.textContent?.includes('Notizen'))!.click();
    fixture.detectChanges();
    const field = el.querySelector<HTMLTextAreaElement>('.case-note-form textarea')!;
    expect(field.value).toBe('Beim Fachteam nachfragen');
    field.value = ' Rückruf am Donnerstag ';
    el.querySelector('form.case-note-form')!.dispatchEvent(new Event('submit', { cancelable: true }));
    expect(changes).toEqual(['Rückruf am Donnerstag']);
  });

  it('sagt beim eigenen bestätigten Fall, dass eine andere Person freigeben muss', () => {
    const { el } = render(detail({ state: 'AWAITING_APPROVAL', confirmedBy: 'gutachterin.a', actions: ['reject'] }));
    expect(el.querySelector('.next-step')!.textContent).toContain('andere Person');
    expect(el.textContent).not.toContain('Freigeben');
  });

  it('zeigt nach Abschluss, dass Inhalte gelöscht sind', () => {
    const { el } = render(detail({ state: 'CLOSED', assessment: null, caseInput: null, documents: [], actions: [] }));
    expect(el.textContent).toContain('gelöscht');
    expect(el.querySelector('.next-step button')).toBeNull();
  });

  it('baut die Falleingabe der Rückfrage exakt, mit Komma als Dezimaltrenner', () => {
    const { fixture, el, actions } = render(detail());
    Array.from(el.querySelectorAll<HTMLButtonElement>('.next-step button')).find((b) => b.textContent!.includes('Angaben ergänzen'))!.click();
    fixture.detectChanges();
    const number = el.querySelector<HTMLInputElement>('#f-score')!;
    number.value = '12,50';
    number.dispatchEvent(new Event('input'));
    el.querySelector<HTMLFormElement>('.clarify-form')!.dispatchEvent(new Event('submit'));

    const action = actions[0] as { kind: 'clarify'; caseInputJson: string };
    expect(action.kind).toBe('clarify');
    expect(action.caseInputJson).toContain('"score":{"kind":"NUMBER","number":12.50}');
    expect(action.caseInputJson).toContain('"request_complete":{"kind":"TRUTH","truth":"YES"}');
    expect(action.caseInputJson).toContain('"supporting_document":"PRESENT"');
    expect(action.caseInputJson).toContain('"assessmentDate":"2026-10-02"');
  });

  it('lehnt ungültige Zahlen ab, statt zu raten', () => {
    const { fixture, el, actions } = render(detail());
    fixture.componentInstance['startClarify']();
    fixture.detectChanges();
    const number = el.querySelector<HTMLInputElement>('#f-score')!;
    number.value = '12.5.3';
    number.dispatchEvent(new Event('input'));
    el.querySelector<HTMLFormElement>('.clarify-form')!.dispatchEvent(new Event('submit'));
    fixture.detectChanges();
    expect(actions).toEqual([]);
    expect(el.querySelector('[role=alert]')!.textContent).toContain('Zahl');
  });

  it('weist nur mit festem Grund zurück', () => {
    const { fixture, el, actions } = render(detail({ state: 'AWAITING_APPROVAL', confirmedBy: 'gutachter.b', actions: ['approve', 'reject'] }), 'freigabe.c');
    Array.from(el.querySelectorAll<HTMLButtonElement>('.next-step button')).find((b) => b.textContent!.includes('Zurückweisen'))!.click();
    fixture.detectChanges();
    expect(el.querySelector('textarea')).toBeNull();
    el.querySelector<HTMLFormElement>('[aria-labelledby=reject-title]')!.dispatchEvent(new Event('submit'));
    expect(actions).toEqual([{ kind: 'reject', reason: 'CRITERIA_UNCLEAR' }]);
  });

  it('zeigt bei der Unterlagensichtung die Dokumente und erklärt, dass keine Regel hinterlegt ist', () => {
    const { fixture, el } = render(detail({ state: 'DOCUMENT_REVIEW', outcome: 'NOT_ASSESSED', assessment: null, knowledgeRelease: null, actions: [] }), 'gutachterin.a');
    expect(el.querySelector('[role=tab][aria-selected=true]')!.textContent).toContain('Dokumente');
    expect(el.querySelector('.next-step')!.textContent).toContain('kein Regelwerk');
    Array.from(el.querySelectorAll<HTMLButtonElement>('[role=tab]')).find((b) => b.textContent!.includes('Prüfung'))!.click();
    fixture.detectChanges();
    expect(el.querySelector('.notice-box')!.textContent).toContain('kein Regelwerk hinterlegt');
  });

  it('nennt bei der technischen Klärung den Fehler statt einer Bewertung', () => {
    const { el } = render(detail({ state: 'TECHNICAL_CLARIFICATION', outcome: 'NOT_ASSESSED', assessment: null, technicalError: 'UNKNOWN_RELEASE', actions: [] }), 'gutachterin.a');
    expect(el.querySelector('.notice-box')!.textContent).toContain('Regelwerk ist nicht bekannt');
    expect(el.querySelector('.outcome-badge')!.textContent).toContain('Keine Bewertung');
  });

  it('springt aus der Begründung an die Fundstelle im Dokument und zeigt die Akte nach Dokumentart', () => {
    const documents = [
      { id: 'auftrag', title: 'Prüfauftrag', mediaType: 'application/pdf', sha256: 'sha256:x', sizeBytes: 2048, pageCount: 2,
        category: 'ORDER', issuedOn: '2026-09-20', references: [{ field: 'request_complete', page: 2 }] },
      { id: 'befund', title: 'Befundbericht', mediaType: 'image/tiff', sha256: 'sha256:y', sizeBytes: 4096, pageCount: 1,
        category: 'MEDICAL', issuedOn: '2026-09-12', references: [] },
    ];
    const { fixture, el } = render(detail({ documents, caseInput: { formatVersion: 1, assessmentDate: '2026-10-02',
      facts: { request_complete: { kind: 'TRUTH', truth: 'YES' } }, evidence: { supporting_document: 'MISSING' } } }));
    const link = el.querySelector<HTMLButtonElement>('.source-link')!;
    expect(link.textContent!.trim()).toBe('Prüfauftrag, S. 2');
    link.click();
    fixture.detectChanges();

    expect(el.querySelector('[role=tab][aria-selected=true]')!.textContent).toContain('Dokumente');
    expect(Array.from(el.querySelectorAll('.file-group h4')).map((h) => h.textContent)).toEqual(['Auftrag und Antrag', 'Ärztliche Unterlagen', 'Fehlende Nachweise']);
    expect(el.querySelector('.file-item[aria-pressed=true]')!.textContent).toContain('in Bewertung verwendet');
    expect(el.querySelector('.reader-toolbar .page-of')!.textContent).toContain('Seite 2 von 2');
    expect(el.querySelector('.sheet.highlighted figcaption')!.textContent).toContain('Seite 2 · Fundstelle');
    expect(el.querySelector('.missing-item')!.textContent).toContain('liegt nicht vor');
  });
});
