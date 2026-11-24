/** Small JSON helpers shared by the read-only views. */
export type Json = Record<string, unknown>;

export const isObject = (value: unknown): value is Json => typeof value === 'object' && value !== null && !Array.isArray(value);
export const isText = (value: unknown): value is string => typeof value === 'string';
export const isNullableText = (value: unknown): value is string | null => value === null || typeof value === 'string';
export const textOr = (value: unknown, fallback: string | null = null): string | null => (isText(value) ? value : fallback);

/**
 * A JSON number kept as its exact source text. Assessment documents carry decimals
 * that must not pass through binary floating point before they are shown.
 */
export class ExactNumber {
  constructor(readonly text: string) {}
}

type ReviverContext = { source?: string } | undefined;

/**
 * Parses JSON and turns every number into an {@link ExactNumber} using the original
 * source text. Older engines without reviver source access are refused instead of
 * silently rounding.
 */
export function parseExact(text: string): unknown {
  let unsupported = false;
  const reviver = function (this: unknown, _key: string, value: unknown, context?: ReviverContext): unknown {
    if (typeof value !== 'number') return value;
    if (!context || typeof context.source !== 'string') { unsupported = true; return value; }
    return new ExactNumber(context.source);
  };
  const parsed = (JSON.parse as (text: string, reviver: unknown) => unknown)(text, reviver);
  if (unsupported) throw new Error('Exact decimal parsing is not available in this browser.');
  return parsed;
}

/** de-DE display of an exact JSON number without rounding: 1234.50 → „1234,50“. */
export function germanDecimal(value: ExactNumber): string {
  const match = /^(-?)(\d+)(?:\.(\d+))?(?:[eE]([+-]?\d+))?$/.exec(value.text);
  if (!match || match[4] !== undefined) return value.text;
  return match[1] + match[2] + (match[3] !== undefined ? ',' + match[3] : '');
}

/** Writes JSON parsed with {@link parseExact} back to text; numbers keep their exact source text. */
export function exactJson(value: unknown, indent = 0, depth = 0): string {
  if (value instanceof ExactNumber) return value.text;
  const pad = indent ? '\n' + ' '.repeat(indent * (depth + 1)) : '';
  const end = indent ? '\n' + ' '.repeat(indent * depth) : '';
  const sep = indent ? ': ' : ':';
  if (Array.isArray(value)) {
    return value.length ? '[' + value.map((v) => pad + exactJson(v, indent, depth + 1)).join(',') + end + ']' : '[]';
  }
  if (isObject(value)) {
    const entries = Object.entries(value);
    return entries.length
      ? '{' + entries.map(([k, v]) => pad + JSON.stringify(k) + sep + exactJson(v, indent, depth + 1)).join(',') + end + '}' : '{}';
  }
  return JSON.stringify(value ?? null);
}
