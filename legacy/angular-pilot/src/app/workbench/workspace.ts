import { isObject, isText } from '../shared/json';

/**
 * Local organisation of the synthetic preview (`/api/demo-workspace`). Folders, colours,
 * bookmarks, notes, confirmation and dispatch are rehearsal state only; they never change
 * an assessment and are not a clinical or legal release.
 */
export interface Folder { id: string; label: string; color: string; parentId: string | null }
export interface Organization { folderId: string | null; color: string | null; bookmark: boolean; note: string; confirmed: boolean; dispatched: boolean }
export interface WorkCase { caseId: string; title: string; queueId: string; canConfirm: boolean; organization: Organization }
export interface AuditEntry { sequence: number; action: string; caseIds: string[] }
export interface Workspace { revision: number; folders: Folder[]; queueColors: Record<string, string>; cases: WorkCase[]; audit: AuditEntry[] }

const COLOR = /^#[0-9a-fA-F]{6}$/;

export function parseWorkspace(value: unknown): Workspace | null {
  if (!isObject(value) || !Number.isInteger(value['revision']) || !Array.isArray(value['folders']) || !Array.isArray(value['cases'])) return null;
  const folders: Folder[] = [];
  for (const f of value['folders']) {
    if (!isObject(f) || !isText(f['id']) || !isText(f['label']) || !isText(f['color']) || !COLOR.test(f['color'])) return null;
    folders.push({ id: f['id'], label: f['label'], color: f['color'], parentId: isText(f['parentId']) ? f['parentId'] : null });
  }
  const cases: WorkCase[] = [];
  for (const c of value['cases']) {
    const o = isObject(c) ? c['organization'] : null;
    if (!isObject(c) || !isText(c['caseId']) || !isText(c['title']) || !isText(c['queueId']) || typeof c['canConfirm'] !== 'boolean' || !isObject(o)) return null;
    if (typeof o['bookmark'] !== 'boolean' || typeof o['confirmed'] !== 'boolean' || typeof o['dispatched'] !== 'boolean') return null;
    cases.push({
      caseId: c['caseId'], title: c['title'], queueId: c['queueId'], canConfirm: c['canConfirm'],
      organization: {
        folderId: isText(o['folderId']) ? o['folderId'] : null,
        color: isText(o['color']) && COLOR.test(o['color']) ? o['color'] : null,
        bookmark: o['bookmark'], note: isText(o['note']) ? o['note'] : '', confirmed: o['confirmed'], dispatched: o['dispatched'],
      },
    });
  }
  const queueColors: Record<string, string> = {};
  if (isObject(value['queueColors'])) for (const [key, color] of Object.entries(value['queueColors'])) if (isText(color) && COLOR.test(color)) queueColors[key] = color;
  const audit: AuditEntry[] = [];
  if (Array.isArray(value['audit'])) for (const a of value['audit']) {
    if (isObject(a) && Number.isInteger(a['sequence']) && isText(a['action']) && Array.isArray(a['caseIds']))
      audit.push({ sequence: a['sequence'] as number, action: a['action'], caseIds: a['caseIds'].filter(isText) });
  }
  return { revision: value['revision'] as number, folders, queueColors, cases, audit };
}

/** Display split of a synthetic title „NC-2026-4103 · Person · Thema“. Presentation only. */
export function caseIdentity(caseId: string, title: string, platformLabel: string): { number: string; person: string; subject: string } {
  const parts = title.split(' · ');
  return parts.length >= 3 && /^NC-\d{4}-\d+$/.test(parts[0])
    ? { number: parts[0], person: parts[1], subject: parts.slice(2).join(' · ') }
    : { number: caseId, person: '', subject: title === 'Synthetischer Arbeitslistenfall' ? platformLabel : title };
}

export type ProcessingStep = 'confirm' | 'blocked' | 'dispatch' | 'complete';
/** Reflects the server's eligibility flags; never derives an approval itself. */
export function processingStep(item: WorkCase): ProcessingStep {
  if (item.organization.dispatched) return 'complete';
  if (item.organization.confirmed) return 'dispatch';
  return item.canConfirm ? 'confirm' : 'blocked';
}
export const canConfirm = (items: WorkCase[]) => items.length > 0 && items.every(c => c.canConfirm && !c.organization.confirmed && !c.organization.dispatched);
export const canDispatch = (items: WorkCase[]) => items.length > 0 && items.every(c => c.organization.confirmed && !c.organization.dispatched);

export const MAX_SELECTION = 100;
/** Range selection over the visible order; `null` when the range exceeds the server limit. */
export function selectRange(visible: string[], anchor: string | null, target: string): string[] | null {
  const end = visible.indexOf(target);
  if (end < 0) return [];
  const start = anchor === null ? -1 : visible.indexOf(anchor);
  if (start < 0) return [target];
  if (Math.abs(end - start) + 1 > MAX_SELECTION) return null;
  return visible.slice(Math.min(start, end), Math.max(start, end) + 1);
}

/** Folders in tree order with their depth, siblings sorted by German collation. */
export function folderTree(folders: Folder[]): (Folder & { depth: number })[] {
  const sorted = [...folders].sort((a, b) => a.label.localeCompare(b.label, 'de-DE', { sensitivity: 'base' }) || a.id.localeCompare(b.id));
  const result: (Folder & { depth: number })[] = [];
  const seen = new Set<string>();
  const append = (parent: string | null, depth: number) => {
    for (const folder of sorted.filter(f => f.parentId === parent)) {
      if (seen.has(folder.id) || depth > 8) continue;
      seen.add(folder.id);
      result.push({ ...folder, depth });
      append(folder.id, depth + 1);
    }
  };
  append(null, 0);
  return result;
}
