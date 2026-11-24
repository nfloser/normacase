import { Injectable } from '@angular/core';
import { ProcessSummary } from './api';
import { PANE_POSITIONS, PanePosition, clampPane } from './pane-resize';

/** Spalten der Fallliste. Die Fachwerte kommen aus der API; hier wird nur angezeigt, sortiert und gefiltert. */
export const COLUMN_IDS = ['subject', 'number', 'status', 'request', 'next', 'result', 'received', 'expires', 'missing', 'documents', 'confirmedBy', 'release', 'system', 'folder'] as const;
export type ColumnId = (typeof COLUMN_IDS)[number];

/** Breite je Spalte in Pixeln; der Vorgang bekommt den Rest, mindestens aber diese Breite. */
export const COLUMN_WIDTHS: Readonly<Record<ColumnId, number>> = {
  subject: 200, number: 130, status: 190, request: 180, next: 180, result: 230, received: 140, expires: 140,
  missing: 120, documents: 90, confirmedBy: 140, release: 210, system: 140, folder: 160,
};

/** Kennung der Plattform-Testfälle (Massenfälle); wie „Plattformfälle einbeziehen“ im bisherigen Fallexplorer. */
export const PLATFORM_SYSTEM = 'plattform-demo';

export const DEFAULT_COLUMNS: readonly ColumnId[] = ['subject', 'number', 'status', 'request', 'next', 'result', 'documents'];

/** Spalten, deren Filter eine Auswahlliste statt Freitext anbietet. */
export const SELECT_COLUMNS: ReadonlySet<ColumnId> = new Set<ColumnId>(['status', 'request', 'result', 'release', 'system', 'folder']);

/**
 * Filter auf diesen Spalten können Personennamen enthalten. Sie gelten nur in der laufenden Sitzung
 * und werden weder im Browser gespeichert noch in Abfragen übernommen.
 */
export const SESSION_ONLY_FILTERS: ReadonlySet<ColumnId> = new Set<ColumnId>(['subject']);

export interface SortState { readonly key: ColumnId; readonly asc: boolean }
export type Filters = Partial<Record<ColumnId, string>>;
export type ColumnSizes = Partial<Record<ColumnId, number>>;
export const MAX_COLUMN_WIDTH = 900;

export function minColumnWidth(c: ColumnId): number {
  return Math.min(COLUMN_WIDTHS[c], 80);
}

export function clampColumnWidth(c: ColumnId, width: number): number {
  return Math.min(MAX_COLUMN_WIDTH, Math.max(minColumnWidth(c), Math.round(width)));
}

export interface SavedQuery {
  readonly name: string;
  readonly queue: string;
  readonly columns: readonly ColumnId[];
  readonly columnSizes: ColumnSizes;
  readonly filters: Filters;
  readonly sort: SortState;
}

export type Density = 'normal' | 'compact' | 'comfortable';
export const DENSITIES: readonly Density[] = ['normal', 'compact', 'comfortable'];

export interface ViewSettings {
  readonly columns: readonly ColumnId[];
  readonly columnSizes: ColumnSizes;
  readonly rowColors: boolean;
  readonly showFilters: boolean;
  /** Vorschau der Fallakte neben der Liste; ausgeblendet hat die Liste den ganzen Platz (F4). */
  readonly showDetails: boolean;
  /** Lage der Vorschau wie im bisherigen Fallexplorer: unten (Standard) oder rechts. */
  readonly previewPosition: PanePosition;
  /** Anteil der Vorschau am Arbeitsbereich in Prozent. */
  readonly previewSize: number;
  /** Zeilendichte der Liste wie im bisherigen Fallexplorer. */
  readonly density: Density;
  /** Explorer (Körbe und Ablage) links ein- oder ausgeblendet. */
  readonly showNav: boolean;
  readonly navPosition: PanePosition;
  readonly navSize: number;
  readonly documentPosition: PanePosition;
  readonly documentSize: number;
  readonly filesPosition: PanePosition;
  readonly filesSize: number;
  readonly showDocuments: boolean;
  readonly showFiles: boolean;
  readonly showDocumentPreview: boolean;
  /** Plattformfälle (Massen- und Technikfälle der Demo) einbeziehen. */
  readonly includePlatform: boolean;
  readonly sort: SortState;
  readonly queries: readonly SavedQuery[];
}

export const DEFAULT_VIEW: ViewSettings = {
  columns: DEFAULT_COLUMNS,
  columnSizes: {},
  rowColors: true,
  showFilters: true,
  showDetails: true,
  previewPosition: 'bottom',
  previewSize: 45,
  density: 'normal',
  showNav: true,
  navPosition: 'left', navSize: 20,
  documentPosition: 'right', documentSize: 55, showDocuments: true,
  filesPosition: 'left', filesSize: 32, showFiles: true, showDocumentPreview: true,
  includePlatform: false,
  sort: { key: 'number', asc: true },
  queries: [],
};

/**
 * Farbton einer Zeile nach Bearbeitungsstand, wie im bisherigen Fallexplorer:
 * Cyan entscheidungsreif, Gelb fehlende Angaben, Kupfer Gegenprüfung, Rot technische Klärung, Petal Unterlagensichtung, Mint versendet.
 */
export function rowTone(p: Pick<ProcessSummary, 'state' | 'outcome'>): 'ready' | 'missing' | 'conflict' | 'technical' | 'documents' | 'approval' | 'done' {
  switch (p.state) {
    case 'IN_CLARIFICATION': return p.outcome === 'HUMAN_REVIEW' ? 'conflict' : 'missing';
    case 'IN_CONFLICT_REVIEW': return 'conflict';
    case 'TECHNICAL_CLARIFICATION': return 'technical';
    case 'DOCUMENT_REVIEW': return 'documents';
    case 'AWAITING_APPROVAL': return 'approval';
    case 'DISPATCHED': case 'CLOSED': return 'done';
    default: return 'ready';
  }
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

function paneSize(v: unknown, fallback: number): number {
  return typeof v === 'number' && Number.isFinite(v) ? clampPane(v) : fallback;
}

function panePosition(v: unknown, fallback: PanePosition): PanePosition {
  return PANE_POSITIONS.find((p) => p === v) ?? fallback;
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

function columnSizes(v: unknown): ColumnSizes {
  const sizes: ColumnSizes = {};
  if (!v || typeof v !== 'object') return sizes;
  for (const [key, value] of Object.entries(v)) {
    if (isColumn(key) && typeof value === 'number' && Number.isFinite(value)) sizes[key] = clampColumnWidth(key, value);
  }
  return sizes;
}

export function sanitize(v: unknown): ViewSettings {
  const o = (v && typeof v === 'object' ? v : {}) as Record<string, unknown>;
  const queries = Array.isArray(o['queries']) ? o['queries'] : [];
  return {
    columns: columns(o['columns']),
    columnSizes: columnSizes(o['columnSizes']),
    rowColors: o['rowColors'] !== false,
    showFilters: o['showFilters'] !== false,
    showDetails: o['showDetails'] !== false,
    previewPosition: panePosition(o['previewPosition'], DEFAULT_VIEW.previewPosition),
    previewSize: paneSize(o['previewSize'], DEFAULT_VIEW.previewSize),
    density: DENSITIES.includes(o['density'] as Density) ? (o['density'] as Density) : 'normal',
    showNav: o['showNav'] !== false,
    navPosition: panePosition(o['navPosition'], DEFAULT_VIEW.navPosition),
    navSize: paneSize(o['navSize'], DEFAULT_VIEW.navSize),
    documentPosition: panePosition(o['documentPosition'], DEFAULT_VIEW.documentPosition),
    documentSize: paneSize(o['documentSize'], DEFAULT_VIEW.documentSize),
    filesPosition: panePosition(o['filesPosition'], DEFAULT_VIEW.filesPosition),
    filesSize: paneSize(o['filesSize'], DEFAULT_VIEW.filesSize),
    showDocuments: o['showDocuments'] !== false,
    showFiles: o['showFiles'] !== false,
    showDocumentPreview: o['showDocumentPreview'] !== false,
    includePlatform: o['includePlatform'] === true,
    sort: sort(o['sort']),
    queries: queries
      .filter((q): q is Record<string, unknown> => !!q && typeof q === 'object' && typeof q['name'] === 'string' && typeof q['queue'] === 'string')
      .slice(0, 20)
      .map((q) => ({
        name: String(q['name']).slice(0, 60),
        queue: String(q['queue']),
        columns: columns(q['columns']),
        columnSizes: columnSizes(q['columnSizes']),
        filters: storableFilters(q['filters']),
        sort: sort(q['sort']),
      })),
  };
}
