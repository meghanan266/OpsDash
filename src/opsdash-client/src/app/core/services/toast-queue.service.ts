import { Injectable, signal } from '@angular/core';
import type { AppToast } from '../models/notification.model';

const defaultDismissMs = 5000;
const maxVisible = 5;

@Injectable({ providedIn: 'root' })
export class ToastQueueService {
  private readonly _toasts = signal<AppToast[]>([]);
  private readonly timers = new Map<string, ReturnType<typeof setTimeout>>();

  readonly toasts = this._toasts.asReadonly();

  push(toast: Omit<AppToast, 'id' | 'createdAt'>, dismissMs = defaultDismissMs): void {
    const id = crypto.randomUUID();
    const full: AppToast = {
      ...toast,
      id,
      createdAt: new Date(),
    };

    this._toasts.update((list) => {
      const next = [full, ...list];
      return next.length > maxVisible ? next.slice(0, maxVisible) : next;
    });

    const t = setTimeout(() => this.dismiss(id), dismissMs);
    this.timers.set(id, t);
  }

  dismiss(id: string): void {
    const t = this.timers.get(id);
    if (t) {
      clearTimeout(t);
      this.timers.delete(id);
    }

    this._toasts.update((list) => list.filter((x) => x.id !== id));
  }
}
