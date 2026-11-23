import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Workbench } from './workbench';
import { detailJson, summary } from './fixtures';

describe('Workbench', () => {
  let http: HttpTestingController;

  beforeEach(() => globalThis.localStorage?.clear());

  async function render(roles = ['gutachter']) {
    TestBed.configureTestingModule({ imports: [Workbench], providers: [provideHttpClient(), provideHttpClientTesting()] });
    http = TestBed.inject(HttpTestingController);
    const fixture = TestBed.createComponent(Workbench);
    fixture.componentRef.setInput('user', { username: 'gutachterin.a', displayName: 'Gutachterin A', roles });
    fixture.detectChanges();
    http.expectOne('/api/v1/system').flush({ product: 'NormaCase', platformVersion: '0.1.0', retentionMode: 'zero-retention' });
    http.expectOne('/api/v1/knowledge/releases').flush([{ releaseId: 'demo-g-2026.1', fields: [{ id: 'request_complete', type: 'truth', required: true }], evidenceRequirements: ['supporting_document'] }]);
    http.expectOne('/api/v1/queues').flush([{ id: 'clarification', count: 1 }, { id: 'review', count: 2 }, { id: 'approval', count: 0 }, { id: 'done', count: 0 }]);
    await fixture.whenStable();
    http.expectOne((r) => r.url === '/api/v1/processes' && r.params.get('queue') === 'review')
      .flush([summary('a'), summary('b', { displayName: 'Andere Person', outcome: 'NOT_SUPPORTED' })]);
    await fixture.whenStable();
    fixture.detectChanges();
    http.expectOne('/api/v1/processes/a').flush(detailJson('a'));
    http.expectOne('/api/v1/knowledge/releases/demo-g-2026.1/presentation')
      .flush({ name: 'Pitch-Demo', fields: { request_complete: { label: 'Pflichtangaben vollständig' } }, evidenceRequirements: { supporting_document: { label: 'Synthetischer Nachweis' } } });
    await fixture.whenStable();
    fixture.detectChanges();
    return fixture;
  }

  afterEach(() => http.verify());

  /** Beantwortet alle offenen Nachlade-Anfragen nach einer Aktion. */
  async function settle(fixture: { whenStable(): Promise<unknown>; detectChanges(): void }) {
    for (let round = 0; round < 5; round++) {
      await fixture.whenStable();
      fixture.detectChanges();
      const open = http.match(() => true);
      if (!open.length) return;
      for (const r of open) {
        const url = r.request.url;
        if (url === '/api/v1/queues') r.flush([]);
        else if (url === '/api/v1/processes') r.flush([summary('a'), summary('b')]);
        else if (url.startsWith('/api/v1/processes/')) r.flush(detailJson(url.split('/').pop()!));
        else r.flush({});
      }
    }
  }

  it('zeigt Körbe mit Anzahl, Vorgänge wie ein Postfach und die Fallakte rechts', async () => {
    const fixture = await render();
    const el = fixture.nativeElement as HTMLElement;

    expect(el.querySelector('.explorer')!.textContent).toContain('Zur Prüfung');
    expect(el.querySelector('.nav-item[aria-current=true] .nav-count')!.textContent).toBe('2');
    expect(el.querySelectorAll('.case-table tbody tr').length).toBe(2);
    expect(el.querySelector('.case-title')!.textContent).toContain('Synthetische Person a');
    expect(el.querySelector('.outcome-badge')!.textContent).toContain('Voraussetzungen erfüllt');
    expect(el.querySelector('.tree')!.textContent).toContain('Pflichtangaben vollständig');
    expect(el.textContent).not.toMatch(/\bSUPPORTED\b|IN_REVIEW|request_complete/);
  });

  it('filtert per Suche', async () => {
    const fixture = await render();
    const el = fixture.nativeElement as HTMLElement;
    const search = el.querySelector<HTMLInputElement>('input[type=search]')!;
    search.value = 'andere';
    search.dispatchEvent(new Event('input'));
    fixture.detectChanges();
    expect(el.querySelectorAll('.case-table tbody tr').length).toBe(1);
  });

  it('bestätigt die Auswahl gesammelt und lädt neu', async () => {
    const fixture = await render();
    const el = fixture.nativeElement as HTMLElement;
    el.querySelector<HTMLInputElement>('thead input[type=checkbox]')!.click();
    fixture.detectChanges();
    const button = Array.from(el.querySelectorAll<HTMLButtonElement>('.commandbar button')).find((b) => b.textContent!.includes('Auswahl bestätigen'))!;
    expect(button.disabled).toBe(false);
    button.click();

    const request = http.expectOne('/api/v1/processes/confirmation');
    expect(request.request.body).toEqual({ processIds: ['a', 'b'] });
    request.flush([{ processId: 'a', result: 'CONFIRMED' }, { processId: 'b', result: 'CONFIRMED' }]);
    await settle(fixture);
    expect(el.querySelector('.toast')!.textContent).toContain('2 von 2');
  });

  it('bestätigt aus der Fallakte', async () => {
    const fixture = await render();
    const el = fixture.nativeElement as HTMLElement;
    Array.from(el.querySelectorAll<HTMLButtonElement>('.next-step button')).find((b) => b.textContent!.includes('Bestätigen'))!.click();
    http.expectOne('/api/v1/processes/a/confirmation').flush(null, { status: 204, statusText: 'No Content' });
    await settle(fixture);
    expect(el.querySelector('.toast')!.textContent).toContain('bestätigt');
  });

  it('meldet das Vier-Augen-Prinzip verständlich', async () => {
    const fixture = await render(['gutachter', 'freigabe']);
    fixture.componentInstance['act']('a', { kind: 'approve' });
    http.expectOne('/api/v1/processes/a/approval')
      .flush({ type: 'https://normacase.local/problems/four-eyes', status: 409 }, { status: 409, statusText: 'Conflict' });
    await settle(fixture);
    expect((fixture.nativeElement as HTMLElement).querySelector('[role=alert]')!.textContent).toContain('nicht selbst freigeben');
  });

  it('bedient die Liste per Tastatur', async () => {
    const fixture = await render();
    const el = fixture.nativeElement as HTMLElement;
    el.querySelector<HTMLElement>('tbody tr')!.dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowDown' }));
    fixture.detectChanges();
    expect(el.querySelectorAll('tbody tr')[1].classList).toContain('active');
    await settle(fixture);
    el.querySelectorAll<HTMLElement>('tbody tr')[1].dispatchEvent(new KeyboardEvent('keydown', { key: ' ' }));
    fixture.detectChanges();
    expect(el.querySelectorAll('tbody tr')[1].getAttribute('data-selected')).toBe('true');
  });

  it('färbt Zeilen standardmäßig und lässt sich abschalten', async () => {
    const fixture = await render();
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('.case-table')!.classList).toContain('row-colors');
    expect(el.querySelector('tbody tr')!.getAttribute('data-tone')).toBe('ready');
    const toggle = Array.from(el.querySelectorAll<HTMLLabelElement>('.commandbar label')).find((l) => l.textContent!.includes('Zeilenfärbung'))!.querySelector('input')!;
    toggle.click();
    fixture.detectChanges();
    expect(el.querySelector('.case-table')!.classList).not.toContain('row-colors');
    expect(JSON.parse(localStorage.getItem('normacase.workbench.view.v1.gutachterin.a')!).rowColors).toBe(false);
  });

  it('passt Spalten über die Spaltenoptionen an', async () => {
    const fixture = await render();
    const el = fixture.nativeElement as HTMLElement;
    const headers = () => Array.from(el.querySelectorAll('.case-table thead tr:first-child th')).map((th) => th.textContent!.trim()).filter(Boolean);
    expect(headers()).toEqual(['Vorgang', 'Vorgangsnummer', 'Bearbeitungsstand', 'Ergebnis', 'Eingang', 'Dokumente']);

    Array.from(el.querySelectorAll<HTMLButtonElement>('.commandbar button')).find((b) => b.textContent!.includes('Spaltenoptionen'))!.click();
    fixture.detectChanges();
    el.querySelector<HTMLButtonElement>('[aria-label="Vorgangsnummer nach oben"]')!.click();
    el.querySelector<HTMLButtonElement>('[aria-label="Dokumente entfernen"]')!.click();
    fixture.detectChanges();
    const add = el.querySelector<HTMLSelectElement>('.column-options select')!;
    add.value = 'release';
    add.dispatchEvent(new Event('change'));
    fixture.detectChanges();
    el.querySelector<HTMLButtonElement>('.column-options button[type=submit]')!.click();
    fixture.detectChanges();

    expect(headers()).toEqual(['Vorgangsnummer', 'Vorgang', 'Bearbeitungsstand', 'Ergebnis', 'Eingang', 'Regelwerk']);
    expect(el.querySelector('tbody td.col-release')!.textContent).toContain('demo-g-2026.1');
  });

  it('filtert je Spalte und speichert die Ansicht als Abfrage ohne Namensfilter', async () => {
    const fixture = await render();
    const el = fixture.nativeElement as HTMLElement;
    Array.from(el.querySelectorAll<HTMLButtonElement>('.commandbar button')).find((b) => b.textContent!.includes('Filter'))!.click();
    fixture.detectChanges();
    const result = el.querySelector<HTMLSelectElement>('select[aria-label="Filter für Ergebnis"]')!;
    result.value = 'Voraussetzungen nicht erfüllt';
    result.dispatchEvent(new Event('change'));
    const name = el.querySelector<HTMLInputElement>('input[aria-label="Filter für Vorgang"]')!;
    name.value = 'andere';
    name.dispatchEvent(new Event('input'));
    fixture.detectChanges();
    expect(el.querySelectorAll('.case-table tbody tr').length).toBe(1);

    fixture.componentInstance['saveQuery']('Nicht erfüllt');
    fixture.detectChanges();
    const stored = JSON.parse(localStorage.getItem('normacase.workbench.view.v1.gutachterin.a')!);
    expect(stored.queries[0]).toMatchObject({ name: 'Nicht erfüllt', queue: 'review', filters: { result: 'Voraussetzungen nicht erfüllt' } });
    expect(JSON.stringify(stored)).not.toContain('andere');
    expect(el.querySelector('.explorer')!.textContent).toContain('Nicht erfüllt');
  });
});
