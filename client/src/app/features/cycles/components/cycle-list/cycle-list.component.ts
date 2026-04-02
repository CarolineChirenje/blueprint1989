import { Component, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { MatDialog } from '@angular/material/dialog';
import { ExpenseCycleService } from '../../../../core/services/expense-cycle.service';
import { ExpenseCycleSummaryDto } from '../../../../shared/models/expense-cycle.model';
import { AuthService } from '../../../../core/services/auth.service';
import { CreateCycleDialogComponent } from '../create-cycle-dialog/create-cycle-dialog.component';

@Component({
  selector: 'app-cycle-list',
  templateUrl: './cycle-list.component.html',
  styleUrls: ['./cycle-list.component.css'],
  standalone: false
})
export class CycleListComponent implements OnInit {
  cycles: ExpenseCycleSummaryDto[] = [];
  loading = true;
  error = '';
  isAdmin = false;

  constructor(
    private cycleService: ExpenseCycleService,
    private auth: AuthService,
    private router: Router,
    private dialog: MatDialog
  ) {}

  ngOnInit(): void {
    this.isAdmin = this.auth.isAdminOrAbove();
    this.load();
  }

  load(): void {
    this.loading = true;
    this.error = '';
    this.cycleService.getAll().subscribe({
      next: cycles => { this.cycles = cycles; this.loading = false; },
      error: () => { this.error = 'Failed to load cycles.'; this.loading = false; }
    });
  }

  openCreate(): void {
    const ref = this.dialog.open(CreateCycleDialogComponent, { width: '520px' });
    ref.afterClosed().subscribe(result => { if (result) this.load(); });
  }

  viewDetail(id: number): void {
    this.router.navigate(['/cycles', id]);
  }

  statusClass(status: string): string {
    return status === 'Active' ? 'badge-active' : 'badge-closed';
  }
}
