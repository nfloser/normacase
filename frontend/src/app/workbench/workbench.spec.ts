import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Workbench } from './workbench';
import { detailJson, summary } from './fixtures';
import { Workspace } from './api';

describe('Workbench', () => {
  let http: HttpTestingController;
  let workspace: Workspace;

  beforeEach(() => {
    globalThis.localStorage?.clear();
    workspace = { folders: [], marks: [], queueColors: {} };
  });

  async function render(roles = ['gutachter']) {
    TestBed.configureTestingModule({ imports: [Workbench], providers: [provideHttpClient(), provideHttpClientTesting()] });
    http = TestBed.inject(HttpTestingController);
    const fixture = TestBed.createComponent(Workbench);
    fixture.componentRef.setInput('user', { username: 'gutachterin.a', displayName: 'Gutachterin A', roles });
    fixture.detectChanges();
    http.expectOne('/api/v1/system').flush({ product: 'NormaCase', platformVersion: '0.1.0', retentionMode: 'zero-retention' });
    http.expectOne('/api/v1/knowledge/releases').flush([{ releaseId: 'demo-g-2026.1', fields: [{ id: 'request_complete', type: 'truth', required: true }], evidenceRequirements: ['supporting_document'] }]);
    await fixture.whenStable();
    http.expectOne('/api/v1/workspace').flush(workspace);
    // Start ist „Alle Vorgänge“: alle vier Körbe werden geladen.
    for (const r of http.match((req) => req.url === '/api/v1/processes')) {
      const queue = r.request.params.get('queue');
      r.flush(queue === 'review'
        ? [summary('a'), summary('b', { displayName: 'Andere Person', outcome: 'NOT_SUPPORTED' })]
        : queue === 'technical'
          ? [summary('p', { externalReference: { system: 'plattform-demo', caseId: 'PF-2026-0001' }, state: 'TECHNICAL_CLARIFICATION', outcome: 'NOT_ASSESSED' })]
          : []);
    }
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
        if (url === '/api/v1/processes') r.flush(r.request.params.get('queue') === 'review' ? [summary('a'), summary('b')] : []);
        else if (url.includes('/documents/')) r.flush(new Blob(['x'], { type: 'image/png' }));
        else if (url.startsWith('/api/v1/processes/')) r.flush(detailJson(url.split('/').pop()!));
        else if (url === '/api/v1/workspace') r.flush(workspace);
        else r.flush({});
      }
    }
  }

  it('zeigt Arbeitslisten und Ablage wie im bisherigen Fallexplorer, Vorgänge und die Fallakte', async () => {
    const fixture = await render();
    const el = fixture.nativeElement as HTMLElement;

    const nav = el.querySelector('.explorer')!.textContent!;
    for (const list of ['Zur Freigabe vorbereitet', 'Informationen nachfordern', 'Gegenprüfung erforderlich', 'Technische Klärung',
      'Unterlagensichtung', 'Meine Ablage', 'Bestätigt', 'Versendet']) {
      expect(nav).toContain(list);
    }
    expect(el.querySelector('.nav-item[aria-current=true]')!.textContent).toContain('Alle Vorgänge');
    expect(el.querySelector('.nav-item[aria-current=true] .nav-count')!.textContent).toBe('2');
    expect(el.querySelector('tbody td.col-request')!.textContent).toContain('Pitch-Demo');
    expect(el.querySelector('tbody td.col-next')!.textContent).toContain('Ergebnis „erfüllt“ prüfen');
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
    const button = Array.from(el.querySelectorAll<HTMLButtonElement>('.commandbar button')).find((b) => b.getAttribute('title') === 'Auswahl bestätigen')!;
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
    expect(JSON.parse(localStorage.getItem('normacase.workbench.view.v2.gutachterin.a')!).rowColors).toBe(false);
  });

  it('passt Spalten über die Spaltenoptionen an', async () => {
    const fixture = await render();
    const el = fixture.nativeElement as HTMLElement;
    const headers = () => Array.from(el.querySelectorAll('.case-table thead tr:first-child th')).map((th) => th.textContent!.trim()).filter(Boolean);
    expect(headers()).toEqual(['Fall / Auftrag', 'Vorgangsnummer', 'Bearbeitungsstand', 'Prüfanlass', 'Nächster Schritt', 'Prüfergebnis', 'Dokumente']);

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

    expect(headers()).toEqual(['Vorgangsnummer', 'Fall / Auftrag', 'Bearbeitungsstand', 'Prüfanlass', 'Nächster Schritt', 'Prüfergebnis', 'Regelwerk']);
    expect(el.querySelector('tbody td.col-release')!.textContent).toContain('demo-g-2026.1');
  });

  it('filtert je Spalte und speichert die Ansicht als Abfrage ohne Namensfilter', async () => {
    const fixture = await render();
    const el = fixture.nativeElement as HTMLElement;
    // Die Filterzeile ist wie im bisherigen Fallexplorer von Anfang an sichtbar.
    const result = el.querySelector<HTMLSelectElement>('select[aria-label="Filter für Prüfergebnis"]')!;
    result.value = 'Voraussetzungen nicht erfüllt';
    result.dispatchEvent(new Event('change'));
    const name = el.querySelector<HTMLInputElement>('input[aria-label="Filter für Fall / Auftrag"]')!;
    name.value = 'andere';
    name.dispatchEvent(new Event('input'));
    fixture.detectChanges();
    expect(el.querySelectorAll('.case-table tbody tr').length).toBe(1);

    fixture.componentInstance['saveQuery']('Nicht erfüllt');
    fixture.detectChanges();
    const stored = JSON.parse(localStorage.getItem('normacase.workbench.view.v2.gutachterin.a')!);
    expect(stored.queries[0]).toMatchObject({ name: 'Nicht erfüllt', queue: 'all', filters: { result: 'Voraussetzungen nicht erfüllt' } });
    expect(JSON.stringify(stored)).not.toContain('andere');
    expect(el.querySelector('.explorer')!.textContent).toContain('Nicht erfüllt');
  });

  it('blendet die Vorschau mit F4 aus, öffnet mit Doppelklick und kehrt zurück', async () => {
    const fixture = await render();
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('.list-and-pane')!.getAttribute('data-pane')).toBe('bottom');
    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'F4' }));
    fixture.detectChanges();
    expect(el.querySelector('.reading-pane')).toBeNull();
    expect(Array.from(el.querySelectorAll('.commandbar button')).some((b) => b.getAttribute('title') === 'Details einblenden (F4)' && b.getAttribute('aria-pressed') === 'false')).toBe(true);

    el.querySelector<HTMLElement>('tbody tr')!.dispatchEvent(new MouseEvent('dblclick', { bubbles: true }));
    await settle(fixture);
    expect(el.querySelector('.opened-case .reading-pane')).not.toBeNull();
    expect(el.querySelector('.case-table')).toBeNull();
    expect(el.querySelector('.tab-panel.split .split-right')).not.toBeNull();

    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowLeft', altKey: true }));
    await settle(fixture);
    expect(el.querySelector('.case-table')).not.toBeNull();
  });

  it('wählt mit Strg und Umschalt mehrere Vorgänge', async () => {
    const fixture = await render();
    const el = fixture.nativeElement as HTMLElement;
    const rows = () => el.querySelectorAll<HTMLElement>('.case-table tbody tr');
    rows()[0].dispatchEvent(new MouseEvent('click', { bubbles: true }));
    rows()[1].dispatchEvent(new MouseEvent('click', { bubbles: true, shiftKey: true }));
    await settle(fixture);
    expect(Array.from(rows()).every((r) => r.getAttribute('data-selected') === 'true')).toBe(true);
    rows()[0].dispatchEvent(new MouseEvent('click', { bubbles: true, ctrlKey: true }));
    fixture.detectChanges();
    expect(rows()[0].getAttribute('data-selected')).toBe('false');
    await settle(fixture);
  });

  it('bedient die Menüleiste wie im bisherigen Fallexplorer', async () => {
    const fixture = await render();
    const el = fixture.nativeElement as HTMLElement;
    const item = (label: string) => Array.from(el.querySelectorAll<HTMLButtonElement>('.menubar .menu-list button')).find((b) => b.textContent!.includes(label))!;
    expect(Array.from(el.querySelectorAll('.menubar summary')).map((s) => s.textContent!.trim())).toEqual(['Datei', 'Bearbeiten', 'Ansicht', 'Fälle', 'Extras', 'Hilfe']);

    item('Alle sichtbaren auswählen').click();
    fixture.detectChanges();
    expect(el.querySelectorAll('.case-table tbody tr[data-selected=true]').length).toBe(2);
    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }));
    fixture.detectChanges();
    expect(el.querySelectorAll('.case-table tbody tr[data-selected=true]').length).toBe(0);

    item('Zeilendichte').click();
    item('Explorer ein/aus').click();
    fixture.detectChanges();
    expect(el.querySelector('.case-table')!.getAttribute('data-density')).toBe('compact');
    expect(el.classList).toContain('nav-collapsed');
    item('Ansicht zurücksetzen').click();
    fixture.detectChanges();
    expect(el.classList).not.toContain('nav-collapsed');
    await settle(fixture);
  });

  it('blendet Plattformfälle nur auf Wunsch ein, Zähler eingeschlossen', async () => {
    const fixture = await render();
    const el = fixture.nativeElement as HTMLElement;
    const technical = () => Array.from(el.querySelectorAll('.nav-item')).find((b) => b.textContent!.includes('Technische Klärung'))!;
    expect(technical().querySelector('.nav-count')!.textContent).toBe('0');
    expect(el.textContent).not.toContain('PF-2026-0001');
    el.querySelector<HTMLInputElement>('.nav-toggle input')!.click();
    fixture.detectChanges();
    expect(technical().querySelector('.nav-count')!.textContent).toBe('1');
    expect(el.querySelector('.case-table')!.textContent).toContain('PF-2026-0001');
    await settle(fixture);
  });

  it('führt Meine Ablage wie im bisherigen Fallexplorer: Ordner, Lesezeichen, Markierung, Notiz, Kontextmenü', async () => {
    workspace = {
      folders: [{ id: 'f1', parentId: null, label: 'Rückfragen', color: '#497ab7' }, { id: 'f2', parentId: 'f1', label: 'Diese Woche', color: '#24708f' }],
      marks: [{ processId: 'b', folderId: 'f2', color: '#a54866', bookmark: true, note: 'Befund folgt' }],
      queueColors: { review: '#287554' },
    };
    const fixture = await render();
    const el: HTMLElement = fixture.nativeElement;
    const nav = () => Array.from(el.querySelectorAll<HTMLButtonElement>('.explorer .nav-item'));
    const navText = () => nav().map((b) => `${b.querySelector('.nav-label')?.textContent?.trim()} ${b.querySelector('.nav-count')?.textContent?.trim()}`);

    // „Alle Vorgänge“ zeigt, was nicht in einem eigenen Ordner liegt; die Arbeitsliste zeigt alles.
    expect(navText()).toContain('Alle Vorgänge 1');
    expect(navText()).toContain('Zur Freigabe vorbereitet 2');
    expect(navText()).toContain('Lesezeichen 1');
    expect(navText()).toContain('Diese Woche 1');
    expect(navText()).toContain('Ohne eigenen Ordner 1');
    expect(el.querySelector<HTMLElement>('.explorer .nav-folder:nth-of-type(2)')?.style.paddingLeft).toBe('14px');
    expect(el.querySelector<HTMLElement>('.nav-dot[data-queue="review"]')?.style.background).toContain('rgb(40, 117, 84)');

    nav().find((b) => b.textContent?.includes('Diese Woche'))!.click();
    fixture.detectChanges();
    const row = el.querySelector<HTMLElement>('.case-table tbody tr')!;
    expect(row.textContent).toContain('Andere Person');
    expect(row.textContent).toContain('Befund folgt');
    expect(row.querySelector('.bookmark')?.getAttribute('aria-label')).toBe('Mit Lesezeichen');
    expect(row.querySelector('.mark-chip')).not.toBeNull();
    await settle(fixture);

    // Rechtsklick: Kontextmenü mit Untermenüs; „Aus Ordner entfernen“ schickt MOVE ohne Ordner.
    row.dispatchEvent(new MouseEvent('contextmenu', { bubbles: true, cancelable: true, clientX: 40, clientY: 40 }));
    fixture.detectChanges();
    const items = Array.from(el.querySelectorAll<HTMLButtonElement>('.context-menu [role=menuitem]')).map((b) => b.textContent?.trim());
    expect(items).toEqual(expect.arrayContaining(['Öffnen', 'Lesezeichen entfernen', 'Aus Ordner entfernen', 'Notiz …', 'Bestätigen', 'Blau', 'Rückfragen']));
    Array.from(el.querySelectorAll<HTMLButtonElement>('.context-menu [role=menuitem]')).find((b) => b.textContent?.trim() === 'Aus Ordner entfernen')!.click();
    const move = http.expectOne('/api/v1/workspace/marks');
    expect(move.request.body).toEqual({ processIds: ['b'], command: 'MOVE', folderId: null });
    move.flush({ ...workspace, marks: [{ processId: 'b', folderId: null, color: '#a54866', bookmark: true, note: 'Befund folgt' }] });
    fixture.detectChanges();
    expect(el.querySelector('.context-menu')).toBeNull();
    expect(el.querySelectorAll('.case-table tbody tr').length).toBe(0);

    // Neuer Ordner über die Befehlsleiste; danach wird die Ablage neu geladen.
    Array.from(el.querySelectorAll<HTMLButtonElement>('.commandbar button')).find((b) => b.getAttribute('aria-label') === 'Neuer Ordner')!.click();
    fixture.detectChanges();
    const name = el.querySelector<HTMLInputElement>('.ablage-dialog input[type=text]')!;
    name.value = 'Gutachten zurück';
    name.dispatchEvent(new Event('input'));
    el.querySelector<HTMLButtonElement>('.ablage-dialog .preset:nth-child(3)')!.click();
    fixture.detectChanges();
    el.querySelector<HTMLFormElement>('.ablage-dialog form')!.dispatchEvent(new Event('submit'));
    const create = http.expectOne('/api/v1/workspace/folders');
    expect(create.request.body).toEqual({ label: 'Gutachten zurück', color: '#3f8f5a', parentId: null });
    create.flush({ id: 'f3', parentId: null, label: 'Gutachten zurück', color: '#3f8f5a' });
    http.expectOne('/api/v1/workspace').flush(workspace);
    fixture.detectChanges();
    expect(el.querySelector('.ablage-dialog')).toBeNull();
    await settle(fixture);
  });
});
