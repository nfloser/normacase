import { Folder } from './api';

/** Ansichtskennung eines eigenen Ordners in der Navigation. */
export const FOLDER_PREFIX = 'folder:';

/** Farbvorlagen wie im bisherigen Fallexplorer. */
export const COLOR_PRESETS: readonly { id: string; hex: string }[] = [
  // Mitteltöne mit mindestens 3:1 Kontrast auf hellen und dunklen Flächen, passend zur Arbeitslisten-Farbfamilie.
  { id: 'blue', hex: '#4f7fc0' },
  { id: 'teal', hex: '#2f8a99' },
  { id: 'green', hex: '#3f8f5a' },
  { id: 'amber', hex: '#b07a12' },
  { id: 'purple', hex: '#8a6bc1' },
  { id: 'rose', hex: '#c0577c' },
];

/** Vorgabefarben der Arbeitslisten (wie die Punkte in der Navigation). */
export const DEFAULT_QUEUE_COLORS: Readonly<Record<string, string>> = {
  review: '#3d8b52', clarification: '#a17d00', conflict: '#c76a22', technical: '#c0453a', documents: '#7a5aa8',
};

export interface AblageDialog {
  readonly kind: 'folder' | 'rename' | 'delete' | 'move' | 'color' | 'note' | 'queueColor' | 'approve';
  readonly ids: string[];
  readonly folderId: string;
  readonly parentId: string | null;
  readonly label: string;
  readonly color: string;
  readonly note: string;
  readonly queue: string;
}

export type TreeFolder = Folder & { readonly depth: number };

/** Ordner in Baumreihenfolge (alphabetisch je Ebene) mit Tiefe für die Einrückung. */
export function folderTree(folders: readonly Folder[]): TreeFolder[] {
  const sorted = [...folders].sort((a, b) => a.label.localeCompare(b.label, 'de-DE', { sensitivity: 'base' }) || a.id.localeCompare(b.id));
  const out: TreeFolder[] = [];
  const known = new Set(folders.map((f) => f.id));
  const visit = (parent: string | null, depth: number) => {
    for (const f of sorted.filter((x) => (x.parentId && known.has(x.parentId) ? x.parentId : null) === parent)) {
      out.push({ ...f, depth });
      if (depth < 8) visit(f.id, depth + 1);
    }
  };
  visit(null, 0);
  return out;
}

/** Gespeicherte Abfragen dürfen auf Lesezeichen, „Ohne eigenen Ordner“ oder einen vorhandenen Ordner zeigen. */
export function isPersonalView(view: string, folders: readonly Folder[]): boolean {
  return view === 'bookmarks' || view === 'unfiled'
    || (view.startsWith(FOLDER_PREFIX) && folders.some((f) => FOLDER_PREFIX + f.id === view));
}
