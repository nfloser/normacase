import { Injectable } from '@angular/core';
import { ProcessSummary } from './api';

/** Spalten der Fallliste. Die Fachwerte kommen aus der API; hier wird nur angezeigt, sortiert und gefiltert. */
export const COLUMN_IDS = ['subject', 'number', 'status', 'result', 'received', 'expires', 'missing', 'documents', 'confirmedBy', 'release', 'system'] as const;
export type ColumnId = (typeof COLUMN_IDS)[number];

/** Breite je Spalte in Pixeln; der Vorgang bekommt den Rest, mindestens aber diese Breite. */
export const COLUMN_WIDTHS: Readonly<Record<ColumnId, number>> = {
  subject: 260, number: 150, status: 176, result: 212, received: 150, expires: 150,
  missing: 128, documents: 96, confirmedBy: 150, release: 230, system: 150,
};

export const DEFAULT_COLUMNS: readonly ColumnId[] = ['subject', 'number', 'status', 'result', 'received', 'documents'];

/** Spalten, deren Filter eine Auswahlliste statt Freitext anbietet. */
export const SELECT_COLUMNS: ReadonlySet<ColumnId> = new Set<ColumnId>(['status', 'result', 'release', 'system']);

/**
 * Filter auf diesen Spalten können Personennamen enthalten. Sie gelten nur in der laufenden Sitzung
 * und werden weder im Browser gespeichert noch in Abfragen übernommen.
 */
export const SESSION_ONLY_FILTERS: ReadonlySet<ColumnId> = new Set<ColumnId>(['subject']);

export interface SortState { readonly key: ColumnId; readonly asc: boolean }
export type Filters = Partial<Record<ColumnId, string>>;

export interface SavedQuery {
  readonly name: string;
  readonly queue: string;
  readonly columns: readonly ColumnId[];
  readonly filters: Filters;
  readonly sort: SortState;
}

export interface ViewSettings {
  readonly columns: readonly ColumnId[];
  readonly rowColors: boolean;
  readonly showFilters: boolean;
  readonly sort: SortState;
  readonly queries: readonly SavedQuery[];
}

export const DEFAULT_VIEW: ViewSettings = {
  columns: DEFAULT_COLUMNS,
  rowColors: true,
  showFilters: false,
  sort: { key: 'received', asc: true },
  queries: [],
};

/**
 * Farbton einer Zeile nach Bearbeitungsstand, wie im bisherigen Fallexplorer:
 * grün bereit zur Bearbeitung oder erledigt, gelb Angaben fehlen, orange Gegenprüfung durch einen Menschen.
 */
export function rowTone(p: Pick<ProcessSummary, 'state' | 'outcome'>): 'ready' | 'missing' | 'conflict' | 'done' {
  if (p.state === 'IN_CLARIFICATION') return p.outcome === 'HUMAN_REVIEW' ? 'conflict' : 'missing';
  if (p.state === 'DISPATCHED' || p.state === 'CLOSED') return 'done';
  return 'ready';
}

/** Hält die Ansichtseinstellungen je Konto im Browser. Speicher kann fehlen oder gesperrt sein; dann gilt der Standard. */
@Injectable({ providedIn: 'root' })
export class ViewStore {
  load(username: string): ViewSettings {
    try {
      const raw = globalThis.localStorage?.getItem(this.key(username));
      return raw ? sanitize(JSON.parse(raw)) : DEFAULT_VIEW;
    } catch {
      return DEFAULT_VIEW;
    }
  }

  save(username: string, view: ViewSettings): void {
    try {
      globalThis.localStorage?.setItem(this.key(username), JSON.stringify(view));
    } catch {
      // Ohne Speicher bleibt die Ansicht nur für diese Sitzung erhalten.
    }
  }

  private key(username: string): string {
    return `normacase.workbench.view.v1.${username}`;
  }
}

const isColumn = (v: unknown): v is ColumnId => typeof v === 'string' && (COLUMN_IDS as readonly string[]).includes(v);

function columns(v: unknown): ColumnId[] {
  const list = Array.isArray(v) ? [...new Set(v.filter(isColumn))] : [];
  return list.length ? list : [...DEFAULT_COLUMNS];
}

function sort(v: unknown): SortState {
  const s = v as { key?: unknown; asc?: unknown } | null;
  return s && isColumn(s.key) ? { key: s.key, asc: s.asc !== false } : DEFAULT_VIEW.sort;
}

/** Nur bekannte, unkritische Filter übernehmen; Texte begrenzt. */
export function storableFilters(v: unknown): Filters {
  const out: Filters = {};
  if (!v || typeof v !== 'object') return out;
  for (const [k, value] of Object.entries(v)) {
    if (isColumn(k) && !SESSION_ONLY_FILTERS.has(k) && typeof value === 'string' && value.trim()) out[k] = value.slice(0, 100);
  }
  return out;
}

export function sanitize(v: unknown): ViewSettings {
  const o = (v && typeof v === 'object' ? v : {}) as Record<string, unknown>;
  const queries = Array.isArray(o['queries']) ? o['queries'] : [];
  return {
    columns: columns(o['columns']),
    rowColors: o['rowColors'] !== false,
    showFilters: o['showFilters'] === true,
    sort: sort(o['sort']),
    queries: queries
      .filter((q): q is Record<string, unknown> => !!q && typeof q === 'object' && typeof q['name'] === 'string' && typeof q['queue'] === 'string')
      .slice(0, 20)
      .map((q) => ({
        name: String(q['name']).slice(0, 60),
        queue: String(q['queue']),
        columns: columns(q['columns']),
        filters: storableFilters(q['filters']),
        sort: sort(q['sort']),
      })),
  };
}
