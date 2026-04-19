import { DatePipe } from '@angular/common';
import { Component, inject } from '@angular/core';
import { Router } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import type { AppToast } from '../../../core/models/notification.model';
import { ToastQueueService } from '../../../core/services/toast-queue.service';

@Component({
  selector: 'app-toast-container',
  standalone: true,
  imports: [DatePipe, MatIconModule],
  templateUrl: './toast-container.component.html',
  styleUrl: './toast-container.component.scss',
})
export class ToastContainerComponent {
  protected readonly toasts = inject(ToastQueueService);
  private readonly router = inject(Router);

  icon(tone: string): string {
    switch (tone) {
      case 'danger':
        return 'error';
      case 'warning':
        return 'warning';
      case 'success':
        return 'check_circle';
      default:
        return 'info';
    }
  }

  onClick(toast: AppToast): void {
    if (toast.route?.length) {
      void this.router.navigate(toast.route);
    }

    this.toasts.dismiss(toast.id);
  }

  dismiss(id: string, ev: Event): void {
    ev.stopPropagation();
    this.toasts.dismiss(id);
  }
}
