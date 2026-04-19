import { Injectable, inject } from '@angular/core';
import { Router } from '@angular/router';
import { MatSnackBar } from '@angular/material/snack-bar';
import type {
  AlertNotification,
  AnomalyNotification,
  HealthScoreNotification,
  IncidentNotification,
} from '../models/notification.model';
import { DashboardRealtimeBridge } from './dashboard-realtime.bridge';
import { SignalRService } from './signalr.service';
import { ToastQueueService } from './toast-queue.service';

@Injectable({ providedIn: 'root' })
export class NotificationOrchestratorService {
  private readonly signalR = inject(SignalRService);
  private readonly router = inject(Router);
  private readonly snackBar = inject(MatSnackBar);
  private readonly toasts = inject(ToastQueueService);
  private readonly bridge = inject(DashboardRealtimeBridge);

  private started = false;

  async start(): Promise<void> {
    if (this.started) {
      return;
    }

    this.started = true;
    this.signalR.setHandlers({
      onAnomalyDetected: (d) => this.onAnomaly(d),
      onIncidentCreated: (d) => this.onIncidentCreated(d),
      onIncidentUpdated: (d) => this.onIncidentUpdated(d),
      onHealthScoreUpdated: (d) => this.onHealth(d),
      onAlertTriggered: (d) => this.onAlert(d),
    });

    try {
      await this.signalR.startConnection();
    } catch (err) {
      this.started = false;
      // eslint-disable-next-line no-console
      console.error('[SignalR] failed to start', err);
      this.snackBar.open('Real-time connection could not be started.', 'Dismiss', { duration: 5000 });
    }
  }

  async stop(): Promise<void> {
    if (!this.started) {
      return;
    }

    this.started = false;
    await this.signalR.stopConnection();
  }

  private onDashboard(): boolean {
    return this.router.url.split(/[?#]/)[0] === '/dashboard';
  }

  private onAnomaly(data: AnomalyNotification): void {
    this.toasts.push({
      tone: data.severity?.toLowerCase() === 'severe' ? 'danger' : 'warning',
      title: 'Anomaly detected',
      description: `${data.metricName} · ${data.severity} · Z ${Number(data.zScore).toFixed(2)}`,
      route: ['/dashboard'],
    });

    if (this.onDashboard()) {
      this.bridge.anomalyDetected$.next(data);
    }
  }

  private onIncidentCreated(data: IncidentNotification): void {
    this.toasts.push({
      tone: 'danger',
      title: 'New incident',
      description: data.title,
      route: ['/incidents', data.incidentId],
    });

    if (this.onDashboard()) {
      this.bridge.incidentCreated$.next(data);
    }
  }

  private onIncidentUpdated(data: IncidentNotification): void {
    this.toasts.push({
      tone: 'info',
      title: 'Incident updated',
      description: `${data.title} is now ${data.status}`,
      route: ['/incidents', data.incidentId],
    });

    this.bridge.incidentUpdated$.next(data);
  }

  private onHealth(data: HealthScoreNotification): void {
    if (this.onDashboard()) {
      this.bridge.healthScoreUpdated$.next(data);
    }

    if (Number(data.overallScore) < 50) {
      this.toasts.push({
        tone: 'danger',
        title: 'Health score critical',
        description: `Overall score dropped to ${Number(data.overallScore).toFixed(0)}`,
        route: ['/dashboard'],
      });
    }
  }

  private onAlert(data: AlertNotification): void {
    const tone = data.isPredictive ? 'warning' : 'danger';
    this.toasts.push({
      tone,
      title: data.isPredictive ? 'Predictive alert' : 'Alert triggered',
      description: `${data.metricName} ${data.operator} ${data.threshold} (value ${data.metricValue})`,
      route: ['/alerts'],
    });
  }
}
