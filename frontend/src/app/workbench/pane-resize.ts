import { Directive, input, output } from '@angular/core';

export const PANE_POSITIONS = ['top', 'right', 'bottom', 'left'] as const;
export type PanePosition = (typeof PANE_POSITIONS)[number];
export const PANE_MIN = 15;
export const PANE_MAX = 85;

export function clampPane(size: number): number {
  return Math.min(PANE_MAX, Math.max(PANE_MIN, Math.round(size)));
}

@Directive({
  selector: '[ncPaneResize]',
  host: {
    class: 'splitter',
    role: 'separator',
    tabindex: '0',
    '[attr.aria-orientation]': 'horizontal() ? "horizontal" : "vertical"',
    '[attr.aria-valuemin]': 'minimum',
    '[attr.aria-valuemax]': 'maximum',
    '[attr.aria-valuenow]': 'paneSize()',
    '(pointerdown)': 'start($event)',
    '(pointermove)': 'move($event)',
    '(pointerup)': 'end($event)',
    '(pointercancel)': 'end($event)',
    '(lostpointercapture)': 'end($event)',
    '(keydown)': 'keys($event)',
  },
})
export class PaneResize {
  readonly position = input.required<PanePosition>({ alias: 'ncPaneResize' });
  readonly paneSize = input.required<number>();
  readonly resizePane = output<number>();
  protected readonly minimum = PANE_MIN;
  protected readonly maximum = PANE_MAX;
  private pointerId: number | null = null;

  protected horizontal(): boolean {
    return this.position() === 'top' || this.position() === 'bottom';
  }

  protected start(event: PointerEvent): void {
    if (event.button !== 0 || this.pointerId !== null) return;
    const grip = event.currentTarget as HTMLElement;
    grip.setPointerCapture(event.pointerId);
    this.pointerId = event.pointerId;
  }

  protected move(event: PointerEvent): void {
    if (this.pointerId !== event.pointerId) return;
    const box = (event.currentTarget as HTMLElement).parentElement!.getBoundingClientRect();
    const distance = this.horizontal() ? event.clientY - box.top : event.clientX - box.left;
    const length = this.horizontal() ? box.height : box.width;
    const percent = (distance / Math.max(1, length)) * 100;
    const fromStart = this.position() === 'top' || this.position() === 'left';
    this.resizePane.emit(clampPane(fromStart ? percent : 100 - percent));
  }

  protected end(event: PointerEvent): void {
    if (event.pointerId === this.pointerId) this.pointerId = null;
  }

  protected keys(event: KeyboardEvent): void {
    const increase = { top: 'ArrowDown', right: 'ArrowLeft', bottom: 'ArrowUp', left: 'ArrowRight' }[this.position()];
    const decrease = { top: 'ArrowUp', right: 'ArrowRight', bottom: 'ArrowDown', left: 'ArrowLeft' }[this.position()];
    if (event.key !== increase && event.key !== decrease) return;
    event.preventDefault();
    this.resizePane.emit(clampPane(this.paneSize() + (event.key === increase ? 5 : -5)));
  }
}
