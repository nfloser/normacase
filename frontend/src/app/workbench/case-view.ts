import { Injectable } from '@angular/core';
import { ProcessSummary } from './api';

/** Spalten der Fallliste. Die Fachwerte kommen aus der API; hier wird nur angezeigt, sortiert und gefiltert. */
export const COLUMN_IDS = ['subject', 'number', 'status', 'request', 'next', 'result', 'received', 'expires', 'missing', 'documents', 'confirmedBy', 'release', 'system'] as const;
export type ColumnId = (typeof COLUMN_IDS)[number];

/** Breite je Spalte in Pixeln; der Vorgang bekommt den Rest, mindestens aber diese Breite. */
export const COLUMN_WIDTHS: Readonly<Record<ColumnId, number>> = {
  subject: 200, number: 130, status: 150, request: 180, next: 180, result: 230, received: 140, expires: 140,
  missing: 120, documents: 90, confirmedBy: 140, release: 210, system: 140,
};

export const DEFAULT_COLUMNS: readonly ColumnId[] = ['subject', 'number', 'status', 'request', 'next', 'result', 'documents'];

/** Spalten, deren Filter eine Auswahlliste statt Freitext anbietet. */
export const SELECT_COLUMNS: ReadonlySet<ColumnId> = new Set<ColumnId>(['status', 'request', 'result', 'release', 'system']);

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

export type PreviewPosition = 'bottom' | 'right';
export type Density = 'normal' | 'compact' | 'comfortable';
export const DENSITIES: readonly Density[] = ['normal', 'compact', 'comfortable'];
export const PREVIEW_MIN = 25;
export const PREVIEW_MAX = 75;

export interface ViewSettings {
  readonly columns: readonly ColumnId[];
  readonly rowColors: boolean;
  readonly showFilters: boolean;
  /** Vorschau der Fallakte neben der Liste; ausgeblendet hat die Liste den ganzen Platz (F4). */
  readonly showDetails: boolean;
  /** Lage der Vorschau wie im bisherigen Fallexplorer: unten (Standard) oder rechts. */
  readonly previewPosition: PreviewPosition;
  /** Anteil der Vorschau am Arbeitsbereich in Prozent. */
  readonly previewSize: number;
  /** Zeilendichte der Liste wie im bisherigen Fallexplorer. */
  readonly density: Density;
  /** Explorer (Körbe und Ablage) links ein- oder ausgeblendet. */
  readonly showNav: boolean;
  readonly sort: SortState;
  readonly queries: readonly SavedQuery[];
}

export const DEFAULT_VIEW: ViewSettings = {
  columns: DEFAULT_COLUMNS,
  rowColors: true,
  showFilters: true,
  showDetails: true,
  previewPosition: 'bottom',
  previewSize: 45,
  density: 'normal',
  showNav: true,
  sort: { key: 'number', asc: true },
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
    return `normacase.workbench.view.v2.${username}`;
  }
}

export function clampPreview(v: unknown): number {
  const n = typeof v === 'number' && Number.isFinite(v) ? Math.round(v) : DEFAULT_VIEW.previewSize;
  return Math.min(PREVIEW_MAX, Math.max(PREVIEW_MIN, n));
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
    showFilters: o['showFilters'] !== false,
    showDetails: o['showDetails'] !== false,
    previewPosition: o['previewPosition'] === 'right' ? 'right' : 'bottom',
    previewSize: clampPreview(o['previewSize']),
    density: DENSITIES.includes(o['density'] as Density) ? (o['density'] as Density) : 'normal',
    showNav: o['showNav'] !== false,
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
