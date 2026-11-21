import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { QueueId, de } from './de';
import { SystemService, SystemState } from './system/system.service';

@Component({
  selector: 'app-root',
  templateUrl: './app.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class App {
  protected readonly t = de;
  protected readonly queueIds = Object.keys(de.queues) as QueueId[];
  protected readonly selectedQueue = signal<QueueId>('open');
  protected readonly queueTitle = computed(() => de.queues[this.selectedQueue()]);

  protected readonly system = toSignal(inject(SystemService).state(), {
    initialValue: { status: 'loading' } as SystemState,
  });

  protected readonly statusLabel = computed(() => de.system[this.system().status]);

  protected readonly retentionLabel = computed(() => {
    const state = this.system();
    return state.status === 'ok' ? (de.system.retention[state.info.retentionMode] ?? '') : '';
  });

  protected selectQueue(id: QueueId): void {
    this.selectedQueue.set(id);
  }
}
