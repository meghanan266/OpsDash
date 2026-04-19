import { Injectable, inject } from '@angular/core';
import {
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
} from '@microsoft/signalr';
import type {
  AlertNotification,
  AnomalyNotification,
  HealthScoreNotification,
  IncidentNotification,
} from '../models/notification.model';
import { environment } from '../../../environments/environment';
import { AuthService } from './auth.service';

@Injectable({ providedIn: 'root' })
export class SignalRService {
  private readonly auth = inject(AuthService);
  private hubConnection: HubConnection | null = null;

  private onAnomaly?: (data: AnomalyNotification) => void;
  private onIncidentCreated?: (data: IncidentNotification) => void;
  private onIncidentUpdated?: (data: IncidentNotification) => void;
  private onHealthScore?: (data: HealthScoreNotification) => void;
  private onAlert?: (data: AlertNotification) => void;

  setHandlers(handlers: {
    onAnomalyDetected?: (data: AnomalyNotification) => void;
    onIncidentCreated?: (data: IncidentNotification) => void;
    onIncidentUpdated?: (data: IncidentNotification) => void;
    onHealthScoreUpdated?: (data: HealthScoreNotification) => void;
    onAlertTriggered?: (data: AlertNotification) => void;
  }): void {
    this.onAnomaly = handlers.onAnomalyDetected;
    this.onIncidentCreated = handlers.onIncidentCreated;
    this.onIncidentUpdated = handlers.onIncidentUpdated;
    this.onHealthScore = handlers.onHealthScoreUpdated;
    this.onAlert = handlers.onAlertTriggered;
  }

  async startConnection(): Promise<void> {
    if (this.hubConnection?.state === HubConnectionState.Connected) {
      return;
    }

    await this.stopConnection();

    const hubConnection = new HubConnectionBuilder()
      .withUrl(environment.signalRUrl, {
        accessTokenFactory: () => this.auth.getToken() ?? '',
      })
      .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
      .configureLogging(LogLevel.Information)
      .build();

    hubConnection.onreconnecting(() => {
      // eslint-disable-next-line no-console
      console.info('[SignalR] reconnecting…');
    });

    hubConnection.onreconnected(() => {
      // eslint-disable-next-line no-console
      console.info('[SignalR] reconnected');
    });

    hubConnection.onclose(() => {
      // eslint-disable-next-line no-console
      console.info('[SignalR] connection closed');
    });

    hubConnection.on('AnomalyDetected', (data: AnomalyNotification) => this.onAnomaly?.(data));
    hubConnection.on('IncidentCreated', (data: IncidentNotification) => this.onIncidentCreated?.(data));
    hubConnection.on('IncidentUpdated', (data: IncidentNotification) => this.onIncidentUpdated?.(data));
    hubConnection.on('HealthScoreUpdated', (data: HealthScoreNotification) => this.onHealthScore?.(data));
    hubConnection.on('AlertTriggered', (data: AlertNotification) => this.onAlert?.(data));

    this.hubConnection = hubConnection;
    await this.hubConnection.start();
    // eslint-disable-next-line no-console
    console.info('[SignalR] connected');
  }

  async stopConnection(): Promise<void> {
    if (!this.hubConnection) {
      return;
    }

    try {
      await this.hubConnection.stop();
    } finally {
      this.hubConnection = null;
    }
  }
}
