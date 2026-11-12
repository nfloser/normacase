import { Component, OnDestroy, signal } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { Subscription } from 'rxjs';
import { de } from './de';

interface QueueItem { caseId: string; }
interface Queue { queueId: string; items: QueueItem[]; }
interface QueuePage { queues: Queue[]; nextPageCursor: string | null; }

@Component({ selector: 'app-root', imports: [FormsModule], templateUrl: './app.html' })
export class App implements OnDestroy {
  protected readonly text = de;
  protected keyInput = '';
  private key = '';
  private pending?: Subscription;
  protected readonly queues = signal<Queue[]>([]);
  protected readonly cursor = signal<string | null>(null);
  protected readonly busy = signal(false);
  protected readonly error = signal('');
  protected readonly active = signal(false);

  constructor(private readonly http: HttpClient) {}

  protected login(): void {
    const key = this.keyInput;
    this.logout();
    if (!key.trim()) { this.error.set(de.credentialRequired); return; }
    this.key = key;
    this.load(null);
  }

  protected load(cursor: string | null): void {
    if (!this.key || this.busy()) return;
    this.pending?.unsubscribe();
    this.busy.set(true);
    this.error.set('');
    this.queues.set([]);
    this.cursor.set(null);
    const query = cursor === null ? '' : '?afterCaseId=' + encodeURIComponent(cursor);
    this.pending = this.http.get<QueuePage>('/api/review/work-queues' + query, {
      headers: { Authorization: 'Bearer ' + this.key }, credentials: 'omit', cache: 'no-store',
    }).subscribe({
      next: page => {
        if (!page || !Array.isArray(page.queues) || !(page.nextPageCursor === null || typeof page.nextPageCursor === 'string') ||
          !page.queues.every(q => q != null && typeof q.queueId === 'string' && Array.isArray(q.items) && q.items.every(i => i != null && typeof i.caseId === 'string'))) {
          this.active.set(false); this.key = ''; this.error.set(de.invalidResponse); this.busy.set(false); return;
        }
        this.queues.set(page.queues); this.cursor.set(page.nextPageCursor); this.active.set(true); this.busy.set(false);
      },
      error: (err: unknown) => {
        this.busy.set(false);
        if (err instanceof HttpErrorResponse && (err.status === 401 || err.status === 403)) {
          this.key = ''; this.active.set(false);
          this.error.set(err.status === 401 ? de.expired : de.forbidden);
        } else { this.error.set(de.networkError); this.key = ''; this.active.set(false); }
      },
    });
  }

  protected label(id: string): string {
    return Object.hasOwn(de.queueLabels, id) ? de.queueLabels[id] : de.unknownQueue;
  }

  protected logout(): void {
    this.pending?.unsubscribe(); this.pending = undefined; this.key = ''; this.keyInput = '';
    this.queues.set([]); this.cursor.set(null); this.busy.set(false); this.active.set(false); this.error.set('');
  }
  ngOnDestroy(): void { this.logout(); }
}
