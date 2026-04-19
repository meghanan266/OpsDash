import { DecimalPipe } from '@angular/common';
import { Component, computed, input } from '@angular/core';
import { MatCardModule } from '@angular/material/card';
import type { HealthScore } from '../../../../core/models/common.model';

@Component({
  selector: 'app-health-score',
  standalone: true,
  imports: [MatCardModule, DecimalPipe],
  templateUrl: './health-score.component.html',
  styleUrl: './health-score.component.scss',
})
export class HealthScoreComponent {
  /** SVG circle r=52 */
  readonly circumference = 2 * Math.PI * 52;

  readonly healthScore = input<HealthScore | null>(null);

  readonly score = computed(() => {
    const h = this.healthScore();
    return h ? Math.round(h.overallScore) : null;
  });

  readonly bandClass = computed(() => {
    const s = this.score();
    if (s === null) {
      return 'band-unknown';
    }

    if (s >= 80) {
      return 'band-high';
    }

    if (s >= 50) {
      return 'band-mid';
    }

    return 'band-low';
  });

  readonly dashOffset = computed(() => {
    const s = this.score();
    if (s === null) {
      return this.circumference;
    }

    const pct = Math.max(0, Math.min(100, s));
    return this.circumference * (1 - pct / 100);
  });
}
