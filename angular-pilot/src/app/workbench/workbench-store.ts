import { inject, Injectable, OnDestroy, signal } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { catchError, from, map, mergeMap, of, Subscription } from 'rxjs';
import { DocumentCase, parseDocumentCase } from './document-case';
import { parseWorkspace, Workspace } from './workspace';
import { isObject, isText } from '../shared/json';

const TEXT = { responseType: 'text' as const, cache: 'no-store' as const, credentials: 'omit' as const };

export interface CommandResult { ok: boolean; message?: string; packageJson?: string }

/** Loads the synthetic workspace and case files and sends local organisation commands. */
@Injectable()
export class WorkbenchStore implements OnDestroy {
  private readonly http = inject(HttpClient);
  private subscriptions = new Subscription();

  readonly workspace = signal<Workspace | null>(null);
  readonly files = signal<Record<string, DocumentCase>>({});
  readonly state = signal<'loading' | 'ready' | 'unavailable' | 'invalid'>('loading');
  readonly busy = signal(false);

  load(): void {
    this.state.set('loading');
    this.subscriptions.add(this.http.get('/api/demo-workspace', TEXT).subscribe({
      next: body => {
        const parsed = this.parse(body);
        if (!parsed) { this.state.set('invalid'); return; }
        this.workspace.set(parsed);
        this.state.set('ready');
        this.loadFiles(parsed.cases.map(c => c.caseId));
      },
      error: () => this.state.set('unavailable'),
    }));
  }

  refresh(): Promise<boolean> {
    return new Promise(resolve => {
      this.subscriptions.add(this.http.get('/api/demo-workspace', TEXT).subscribe({
        next: body => { const parsed = this.parse(body); if (parsed) this.workspace.set(parsed); resolve(!!parsed); },
        error: () => resolve(false),
      }));
    });
  }

  /** Case files are fetched four at a time; a missing file only leaves its columns empty. */
  private loadFiles(ids: string[]): void {
    const pending = ids.filter(id => !this.files()[id]);
    this.subscriptions.add(from(pending).pipe(
      mergeMap(id => this.http.get(`/api/document-cases/${encodeURIComponent(id)}`, TEXT).pipe(
        map(body => [id, body] as const),
        catchError(() => of([id, null] as const)),
      ), 4),
    ).subscribe(([id, body]) => {
      const file = body === null ? null : parseDocumentCase(body, id);
      if (file) this.files.update(files => ({ ...files, [id]: file }));
    }));
  }

  async command(action: string, caseIds: string[], extra: Record<string, unknown> = {}): Promise<CommandResult> {
    const workspace = this.workspace();
    if (!workspace || this.busy()) return { ok: false };
    this.busy.set(true);
    const body = { action, caseIds, expectedRevision: workspace.revision, operationId: crypto.randomUUID(), ...extra };
    try {
      return await new Promise<CommandResult>(resolve => {
        this.subscriptions.add(this.http.post('/api/demo-workspace/commands', body, TEXT).subscribe({
          next: text => {
            let root: unknown = null;
            try { root = JSON.parse(text); } catch { /* handled below */ }
            const next = isObject(root) ? this.parseValue(root['workspace']) : null;
            if (!next || !isObject(root)) { resolve({ ok: false }); return; }
            this.workspace.set(next);
            resolve({ ok: true, packageJson: isText(root['packageJson']) ? root['packageJson'] : undefined });
          },
          error: async (err: unknown) => {
            let message: string | undefined;
            if (err instanceof HttpErrorResponse && typeof err.error === 'string') {
              try { const parsed = JSON.parse(err.error); if (isObject(parsed) && isText(parsed['message'])) message = parsed['message']; } catch { /* generic */ }
            }
            await this.refresh();
            resolve({ ok: false, message });
          },
        }));
      });
    } finally { this.busy.set(false); }
  }

  private parse(body: string): Workspace | null {
    try { return this.parseValue(JSON.parse(body)); } catch { return null; }
  }
  private parseValue(value: unknown): Workspace | null { return parseWorkspace(value); }

  ngOnDestroy(): void { this.subscriptions.unsubscribe(); }
}
