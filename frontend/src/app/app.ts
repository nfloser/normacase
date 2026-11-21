import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { AuthService } from './auth/auth.service';
import { de } from './de';
import { SystemService, SystemState } from './system/system.service';
import { Workbench } from './workbench/workbench';

@Component({
  selector: 'app-root',
  imports: [Workbench],
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
  protected readonly authError = computed(() => {
    const state = this.auth.state();
    return state.status === 'anonymous' && state.error ? de.auth.errors[state.error] : undefined;
  });
  protected readonly system = toSignal(inject(SystemService).state(), {
    initialValue: { status: 'loading' } as SystemState,
  });
  protected readonly statusLabel = computed(() => de.system[this.system().status]);
}
