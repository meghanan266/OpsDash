import { Component, inject } from '@angular/core';
import { MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';

export interface AuditDiffDialogData {
  oldValues: string | null;
  newValues: string | null;
}

@Component({
  selector: 'app-audit-diff-dialog',
  standalone: true,
  imports: [MatDialogModule, MatButtonModule],
  template: `
    <h2 mat-dialog-title>Changes</h2>
    <mat-dialog-content class="content">
      <div class="col">
        <h3 class="col-title">Previous</h3>
        <pre class="json-block">{{ data.oldValues || '—' }}</pre>
      </div>
      <div class="col">
        <h3 class="col-title">New</h3>
        <pre class="json-block">{{ data.newValues || '—' }}</pre>
      </div>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-flat-button mat-dialog-close type="button">Close</button>
    </mat-dialog-actions>
  `,
  styles: `
    .content {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 16px;
      min-width: min(92vw, 720px);
      max-height: 70vh;
      overflow: auto;
    }

    .col-title {
      margin: 0 0 8px;
      font-size: 0.8rem;
      font-weight: 700;
      color: color-mix(in srgb, var(--mat-sys-on-surface) 72%, transparent);
      text-transform: uppercase;
      letter-spacing: 0.04em;
    }

    .json-block {
      margin: 0;
      padding: 12px;
      border-radius: 8px;
      background: color-mix(in srgb, var(--mat-sys-surface-container) 88%, transparent);
      font-size: 0.72rem;
      line-height: 1.45;
      white-space: pre-wrap;
      word-break: break-word;
      max-height: 52vh;
      overflow: auto;
    }

    @media (max-width: 720px) {
      .content {
        grid-template-columns: 1fr;
      }
    }
  `,
})
export class AuditDiffDialogComponent {
  readonly data = inject<AuditDiffDialogData>(MAT_DIALOG_DATA);
}
