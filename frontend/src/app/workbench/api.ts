import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map, shareReplay } from 'rxjs';
import { isObject, parseExact } from '../shared/json';

export interface QueueCount { readonly id: string; readonly count: number }

export interface ProcessSummary {
  readonly processId: string;
  readonly externalReference: { system: string; caseId: string };
  readonly knowledgeRelease: string;
  readonly state: string;
  readonly outcome: string;
  readonly receivedAt: string;
  readonly expiresAt: string;
  readonly displayName: string | null;
  readonly confirmedBy: string | null;
  readonly documentCount: number;
  readonly missingFieldCount: number;
}

export interface DocumentMeta {
  readonly id: string;
  readonly title: string;
  readonly mediaType: string;
  readonly sha256: string;
  readonly sizeBytes: number;
  readonly pageCount: number;
}

export interface HistoryEntry { readonly at: string; readonly actor: string; readonly action: string; readonly reason: string | null }

/** Fallakte. {@code assessment} und {@code caseInput} sind exakt geparst (Zahlen als ExactNumber). */
export interface ProcessDetail {
  readonly processId: string;
  readonly externalReference: { system: string; caseId: string };
  readonly displayName: string | null;
  readonly knowledgeRelease: string;
  readonly state: string;
  readonly queue: string;
  readonly outcome: string;
  readonly evidenceId: string | null;
  readonly confirmedBy: string | null;
  readonly receivedAt: string;
  readonly expiresAt: string;
  readonly assessment: unknown;
  readonly caseInput: unknown;
  readonly documents: DocumentMeta[];
  readonly history: HistoryEntry[];
  readonly actions: string[];
}

export interface Labels { readonly label: string; readonly helpText?: string }
export interface Presentation {
  readonly name?: string;
  readonly fields?: Record<string, Labels>;
  readonly evidenceRequirements?: Record<string, Labels>;
  readonly outputs?: Record<string, { label: string; choices: Record<string, string> }>;
}

export interface ReleaseSummary {
  readonly releaseId: string;
  readonly fields: { id: string; type: 'truth' | 'number'; required: boolean }[];
  readonly evidenceRequirements: string[];
}

export interface BatchResult { readonly processId: string; readonly result: string }

@Injectable({ providedIn: 'root' })
export class WorkbenchApi {
  private readonly http = inject(HttpClient);
  private readonly presentations = new Map<string, Observable<Presentation>>();
  private releasesCache?: Observable<ReleaseSummary[]>;

  queues(): Observable<QueueCount[]> {
    return this.http.get<QueueCount[]>('/api/v1/queues');
  }

  processes(queue: string): Observable<ProcessSummary[]> {
    return this.http.get<ProcessSummary[]>('/api/v1/processes', { params: { queue } });
  }

  process(id: string): Observable<ProcessDetail> {
    return this.http.get(`/api/v1/processes/${encodeURIComponent(id)}`, { responseType: 'text' }).pipe(
      map((text) => {
        const parsed = parseExact(text);
        if (!isObject(parsed)) throw new Error('Ungültige Fallakte');
        const plain = JSON.parse(text) as ProcessDetail;
        return { ...plain, assessment: parsed['assessment'], caseInput: parsed['caseInput'] };
      }),
    );
  }

  presentation(releaseId: string): Observable<Presentation> {
    let cached = this.presentations.get(releaseId);
    if (!cached) {
      cached = this.http
        .get<Presentation>(`/api/v1/knowledge/releases/${encodeURIComponent(releaseId)}/presentation`)
        .pipe(shareReplay(1));
      this.presentations.set(releaseId, cached);
    }
    return cached;
  }

  releases(): Observable<ReleaseSummary[]> {
    this.releasesCache ??= this.http.get<ReleaseSummary[]>('/api/v1/knowledge/releases').pipe(shareReplay(1));
    return this.releasesCache;
  }

  confirm(id: string): Observable<void> {
    return this.http.post<void>(`/api/v1/processes/${encodeURIComponent(id)}/confirmation`, null);
  }

  confirmBatch(ids: string[]): Observable<BatchResult[]> {
    return this.http.post<BatchResult[]>('/api/v1/processes/confirmation', { processIds: ids });
  }

  approve(id: string): Observable<void> {
    return this.http.post<void>(`/api/v1/processes/${encodeURIComponent(id)}/approval`, null);
  }

  reject(id: string, reason: string): Observable<void> {
    return this.http.post<void>(`/api/v1/processes/${encodeURIComponent(id)}/rejection`, { reason });
  }

  /** Die Falleingabe wird als fertiger JSON-Text gesendet, damit Dezimalzahlen exakt bleiben. */
  clarify(id: string, caseInputJson: string): Observable<unknown> {
    return this.http.post(`/api/v1/processes/${encodeURIComponent(id)}/clarification`, caseInputJson, {
      headers: { 'Content-Type': 'application/json' },
    });
  }

  documentBlob(processId: string, documentId: string): Observable<Blob> {
    return this.http.get(`/api/v1/processes/${encodeURIComponent(processId)}/documents/${encodeURIComponent(documentId)}`,
      { responseType: 'blob' });
  }

  pageBlob(processId: string, documentId: string, page: number): Observable<Blob> {
    return this.http.get(
      `/api/v1/processes/${encodeURIComponent(processId)}/documents/${encodeURIComponent(documentId)}/pages/${page}`,
      { responseType: 'blob' });
  }
}
