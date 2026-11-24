import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { isObject, parseExact } from '../shared/json';

export interface AssessmentResponse {
  readonly evidenceId: string;
  readonly inputSha256: string;
  readonly knowledgeSha256: string;
  /** Bewertungsdokument, exakt geparst (Zahlen als ExactNumber). */
  readonly document: unknown;
}

export interface Verification {
  readonly inputMatches: boolean;
  readonly knowledgeMatches: boolean;
  readonly outcomeMatches: boolean;
  readonly recordedOutcome: string;
  readonly replayedOutcome: string;
  readonly verified: boolean;
}

export interface EvidenceRecord {
  readonly evidenceId: string;
  readonly recordedAt: string;
  readonly externalSystem: string | null;
  readonly externalCaseId: string | null;
  readonly inputSha256: string;
  readonly platformVersion: string;
  readonly knowledgeRelease: string;
  readonly knowledgeSha256: string;
  readonly assessmentDate: string;
  readonly ruleId: string | null;
  readonly ruleVersion: number | null;
  readonly outcome: string;
  readonly actor: string;
  readonly actorKind: string;
}

/** Extras: Regelwerke ausprobieren, Beispiele abgleichen, Nachweise ansehen und nachprüfen. */
@Injectable({ providedIn: 'root' })
export class ExtrasApi {
  private readonly http = inject(HttpClient);

  examples(releaseId: string): Observable<{ id: string; label: string; expectedOutcome: string | null }[]> {
    return this.http.get<{ id: string; label: string; expectedOutcome: string | null }[]>(
      `/api/v1/knowledge/releases/${encodeURIComponent(releaseId)}/examples`);
  }

  /** Beispiel als JSON-Text; Dezimalzahlen bleiben exakt. */
  example(releaseId: string, exampleId: string): Observable<string> {
    return this.http.get(`/api/v1/knowledge/releases/${encodeURIComponent(releaseId)}/examples/${encodeURIComponent(exampleId)}`,
      { responseType: 'text' });
  }

  /** Bewertet ohne Vorgang; es bleibt nur der Entscheidungsnachweis. Die Falleingabe geht als fertiger JSON-Text. */
  assess(releaseId: string, caseJson: string): Observable<AssessmentResponse> {
    const body = `{"knowledgeRelease":${JSON.stringify(releaseId)},"case":${caseJson}}`;
    return this.http.post('/api/v1/assessments', body, { headers: { 'Content-Type': 'application/json' }, responseType: 'text' }).pipe(
      map((text) => {
        const parsed = parseExact(text);
        if (!isObject(parsed)) throw new Error('Ungültige Antwort');
        return {
          evidenceId: String(parsed['evidenceId']),
          inputSha256: String(parsed['inputSha256']),
          knowledgeSha256: String(parsed['knowledgeSha256']),
          document: parsed['document'],
        };
      }),
    );
  }

  verify(evidenceId: string, caseJson: string): Observable<Verification> {
    return this.http.post<Verification>(`/api/v1/evidence/${encodeURIComponent(evidenceId)}/verification`, caseJson,
      { headers: { 'Content-Type': 'application/json' } });
  }

  evidence(system: string, caseId: string): Observable<EvidenceRecord[]> {
    return this.http.get<EvidenceRecord[]>('/api/v1/evidence', { params: { system, caseId } });
  }
}
