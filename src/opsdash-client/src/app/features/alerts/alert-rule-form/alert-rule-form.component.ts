import { Component, DestroyRef, inject, OnInit, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatRadioModule } from '@angular/material/radio';
import { MatSelectModule } from '@angular/material/select';
import { take } from 'rxjs';
import type { AlertRuleRow } from '../models/alert.models';
import { AlertsService } from '../alerts.service';

export interface AlertRuleFormDialogData {
  mode: 'create' | 'edit';
  rule?: AlertRuleRow;
}

@Component({
  selector: 'app-alert-rule-form',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatRadioModule,
    MatButtonModule,
    MatProgressSpinnerModule,
  ],
  templateUrl: './alert-rule-form.component.html',
  styleUrl: './alert-rule-form.component.scss',
})
export class AlertRuleFormComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly alerts = inject(AlertsService);
  private readonly destroyRef = inject(DestroyRef);
  readonly dialogRef = inject(MatDialogRef<AlertRuleFormComponent, AlertRuleRow | undefined>);
  readonly data = inject<AlertRuleFormDialogData>(MAT_DIALOG_DATA);

  readonly saving = signal(false);
  readonly errorMessage = signal<string | null>(null);

  readonly operators = ['GreaterThan', 'LessThan', 'Equals'] as const;

  readonly form = this.fb.group({
    metricName: ['', [Validators.required, Validators.maxLength(200)]],
    operator: ['GreaterThan', [Validators.required]],
    threshold: [0, [Validators.required]],
    alertMode: this.fb.nonNullable.control<'Current' | 'Predictive'>('Current', [Validators.required]),
    forecastHorizon: [null as number | null],
  });

  ngOnInit(): void {
    if (this.data.mode === 'edit' && this.data.rule) {
      const r = this.data.rule;
      this.form.patchValue({
        metricName: r.metricName,
        operator: r.operator,
        threshold: Number(r.threshold),
        alertMode: r.alertMode === 'Predictive' ? 'Predictive' : 'Current',
        forecastHorizon: r.forecastHorizon,
      });
    }

    this.applyForecastValidators(this.form.controls.alertMode.value);

    this.form.controls.alertMode.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((mode) => {
      if (mode !== 'Current' && mode !== 'Predictive') {
        return;
      }

      if (mode === 'Current') {
        this.form.patchValue({ forecastHorizon: null }, { emitEvent: false });
      }

      this.applyForecastValidators(mode);
    });
  }

  cancel(): void {
    this.dialogRef.close();
  }

  save(): void {
    this.errorMessage.set(null);
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const v = this.form.getRawValue();
    const metricName = (v.metricName ?? '').trim();
    const operator = v.operator ?? 'GreaterThan';
    const mode = v.alertMode ?? 'Current';
    const horizon = mode === 'Predictive' ? v.forecastHorizon : null;
    if (mode === 'Predictive' && (horizon === null || horizon === undefined || Number(horizon) < 1)) {
      this.form.controls.forecastHorizon.markAsTouched();
      return;
    }

    this.saving.set(true);

    if (this.data.mode === 'create') {
      this.alerts
        .createAlertRule({
          metricName,
          operator,
          threshold: Number(v.threshold),
          alertMode: mode,
          forecastHorizon: mode === 'Predictive' ? Number(horizon) : null,
        })
        .pipe(take(1))
        .subscribe({
          next: (res) => {
            this.saving.set(false);
            if (res.success && res.data) {
              this.dialogRef.close(res.data);
            } else {
              this.errorMessage.set(res.message ?? 'Could not create alert rule');
            }
          },
          error: () => {
            this.saving.set(false);
            this.errorMessage.set('Could not create alert rule');
          },
        });
      return;
    }

    const id = this.data.rule?.id;
    if (id === undefined) {
      this.saving.set(false);
      this.errorMessage.set('Missing rule id');
      return;
    }

    this.alerts
      .updateAlertRule(id, {
        metricName,
        operator,
        threshold: Number(v.threshold),
        alertMode: mode,
        forecastHorizon: mode === 'Predictive' ? Number(horizon) : null,
      })
      .pipe(take(1))
      .subscribe({
        next: (res) => {
          this.saving.set(false);
          if (res.success && res.data) {
            this.dialogRef.close(res.data);
          } else {
            this.errorMessage.set(res.message ?? 'Could not update alert rule');
          }
        },
        error: () => {
          this.saving.set(false);
          this.errorMessage.set('Could not update alert rule');
        },
      });
  }

  private applyForecastValidators(mode: 'Current' | 'Predictive'): void {
    const fc = this.form.controls.forecastHorizon;
    if (mode === 'Predictive') {
      fc.setValidators([Validators.required, Validators.min(1), Validators.max(365)]);
    } else {
      fc.clearValidators();
    }

    fc.updateValueAndValidity({ emitEvent: false });
  }
}
