import { Injectable } from '@angular/core';
import { Subject } from 'rxjs';
import type { AnomalyNotification, HealthScoreNotification, IncidentNotification } from '../models/notification.model';

/** Lightweight pub/sub for dashboard + incident views (SignalR orchestrator emits here). */
@Injectable({ providedIn: 'root' })
export class DashboardRealtimeBridge {
  readonly anomalyDetected$ = new Subject<AnomalyNotification>();
  readonly incidentCreated$ = new Subject<IncidentNotification>();
  readonly incidentUpdated$ = new Subject<IncidentNotification>();
  readonly healthScoreUpdated$ = new Subject<HealthScoreNotification>();
}
