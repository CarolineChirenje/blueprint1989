import { Component, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { MatDialog } from '@angular/material/dialog';
import { ExpenseCycleService } from '../../../../core/services/expense-cycle.service';
import { ExpenseCycleSummaryDto } from '../../../../shared/models/expense-cycle.model';
import { AuthService } from '../../../../core/services/auth.service';
import { CreateCycleDialogComponent } from '../create-cycle-dialog/create-cycle-dialog.component';
import { GroupService } from '../../../../core/services/group.service';
import { GroupDto } from '../../../../shared/models/group.model';

@Component({
  selector: 'app-cycle-list',
  templateUrl: './cycle-list.component.html',
  styleUrls: ['./cycle-list.component.css'],
  standalone: false
})
export class CycleListComponent implements OnInit {
  cycles: ExpenseCycleSummaryDto[] = [];
  groups: GroupDto[] = [];
  selectedGroupId: number | null = null;
  loading = true;
  error = '';
  isAdmin = false;

  constructor(
    private cycleService: ExpenseCycleService,
    private auth: AuthService,
    private router: Router,
    private dialog: MatDialog,
    private groupService: GroupService
  ) {}

  ngOnInit(): void {
    this.isAdmin = this.auth.isAdminOrAbove();
    this.loadGroups();
    this.load();
  }

  loadGroups(): void {
    this.groupService.getGroups().subscribe({
      next: groups => { this.groups = groups; },
      error: () => {}
    });
  }

  setGroupFilter(groupId: number | null): void {
    this.selectedGroupId = groupId;
    this.load();
  }

  load(): void {
    this.loading = true;
    this.error = '';
    this.cycleService.getAll(this.selectedGroupId).subscribe({
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
