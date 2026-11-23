import { Folder } from './api';

/** Ansichtskennung eines eigenen Ordners in der Navigation. */
export const FOLDER_PREFIX = 'folder:';

/** Farbvorlagen wie im bisherigen Fallexplorer. */
export const COLOR_PRESETS: readonly { id: string; hex: string }[] = [
  { id: 'blue', hex: '#497ab7' },
  { id: 'teal', hex: '#24708f' },
  { id: 'green', hex: '#287554' },
  { id: 'amber', hex: '#b7791f' },
  { id: 'purple', hex: '#7956a3' },
  { id: 'rose', hex: '#a54866' },
];

/** Vorgabefarben der Arbeitslisten (wie die Punkte in der Navigation). */
export const DEFAULT_QUEUE_COLORS: Readonly<Record<string, string>> = {
  review: '#24708f', clarification: '#b7791f', conflict: '#a54866', technical: '#7956a3', documents: '#497ab7',
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
