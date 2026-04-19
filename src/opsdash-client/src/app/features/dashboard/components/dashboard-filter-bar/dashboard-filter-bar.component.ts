import { Component, input, model, output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';

@Component({
  selector: 'app-dashboard-filter-bar',
  standalone: true,
  imports: [FormsModule, MatFormFieldModule, MatInputModule, MatSelectModule, MatButtonModule, MatIconModule],
  templateUrl: './dashboard-filter-bar.component.html',
  styleUrl: './dashboard-filter-bar.component.scss',
})
export class DashboardFilterBarComponent {
  readonly categories = input<string[]>([]);

  readonly startDate = model<string>('');
  readonly endDate = model<string>('');
  readonly category = model<string | null>(null);

  readonly refresh = output<void>();
  readonly exportCsv = output<void>();

  emitRefresh(): void {
    this.refresh.emit();
  }

  emitExportCsv(): void {
    this.exportCsv.emit();
  }
}
