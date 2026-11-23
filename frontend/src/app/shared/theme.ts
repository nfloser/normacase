import { Injectable, signal } from '@angular/core';

export type ThemeChoice = 'system' | 'light' | 'dark';
const KEY = 'normacase.theme';

/**
 * Hell, Dunkel oder wie das Betriebssystem. Die Wahl gilt für das Gerät und wird im Browser gemerkt;
 * sie enthält keine Fall- oder Personendaten.
 */
@Injectable({ providedIn: 'root' })
export class ThemeStore {
  readonly choice = signal<ThemeChoice>(read());

  constructor() {
    apply(this.choice());
  }

  set(choice: ThemeChoice): void {
    this.choice.set(choice);
    apply(choice);
    try {
      globalThis.localStorage?.setItem(KEY, choice);
    } catch {
      // Ohne Speicher gilt die Wahl nur für diese Sitzung.
    }
  }
}

function read(): ThemeChoice {
  try {
    const v = globalThis.localStorage?.getItem(KEY);
    return v === 'light' || v === 'dark' ? v : 'system';
  } catch {
    return 'system';
  }
}

function apply(choice: ThemeChoice): void {
  const root = globalThis.document?.documentElement;
  if (!root) return;
  if (choice === 'system') root.removeAttribute('data-theme');
  else root.setAttribute('data-theme', choice);
}
