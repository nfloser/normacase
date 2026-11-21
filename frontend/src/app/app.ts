import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { QueueId, de } from './de';
import { AuthService } from './auth/auth.service';
import { SystemService, SystemState } from './system/system.service';

@Component({
  selector: 'app-root',
  templateUrl: './app.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class App {
  protected readonly t = de;
  protected readonly auth = inject(AuthService);
  protected readonly user = computed(() => {
    const state = this.auth.state();
    return state.status === 'authenticated' ? state.user : undefined;
  });
  protected readonly roleLabels = computed(() =>
    (this.user()?.roles ?? []).map((r) => de.roles[r]).filter((r): r is string => !!r).join(', '),
  );
  protected readonly authError = computed(() => {
    const state = this.auth.state();
    return state.status === 'anonymous' && state.error ? de.auth.errors[state.error] : undefined;
  });
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
