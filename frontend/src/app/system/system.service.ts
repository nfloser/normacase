import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, map, of, startWith } from 'rxjs';

export interface SystemInfo {
  readonly product: string;
  readonly platformVersion: string;
  readonly retentionMode: string;
}

export type SystemState =
  | { readonly status: 'loading' }
  | { readonly status: 'ok'; readonly info: SystemInfo }
  | { readonly status: 'offline' };

@Injectable({ providedIn: 'root' })
export class SystemService {
  private readonly http = inject(HttpClient);

  /** Zustand der Backend-Verbindung; Fehler werden zu einem verständlichen Zustand, nie zu einer Ausnahme. */
  state(): Observable<SystemState> {
    return this.http.get<SystemInfo>('/api/v1/system').pipe(
      map((info): SystemState => ({ status: 'ok', info })),
      catchError(() => of<SystemState>({ status: 'offline' })),
      startWith<SystemState>({ status: 'loading' }),
    );
  }
}
