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
});
