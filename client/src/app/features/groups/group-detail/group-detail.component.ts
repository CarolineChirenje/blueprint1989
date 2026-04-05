import { ChangeDetectorRef, Component, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { MatDialog } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';
import { forkJoin } from 'rxjs';
import { GroupService } from '../../../core/services/group.service';
import { ExpenseCycleService } from '../../../core/services/expense-cycle.service';
import { AuthService } from '../../../core/services/auth.service';
import { DialogService } from '../../../shared/services/dialog.service';
import { GroupDetailDto, GroupMemberDto } from '../../../shared/models/group.model';
import { ExpenseCycleSummaryDto } from '../../../shared/models/expense-cycle.model';
import { CreateGroupDialogComponent } from '../create-group-dialog/create-group-dialog.component';
import { GroupMembersDialogComponent } from '../group-members-dialog/group-members-dialog.component';
import { CreateCycleDialogComponent } from '../../cycles/components/create-cycle-dialog/create-cycle-dialog.component';

@Component({
  selector: 'app-group-detail',
  templateUrl: './group-detail.component.html',
  styleUrls: ['./group-detail.component.css', '../../../shared/styles/table.css'],
  standalone: false
})
export class GroupDetailComponent implements OnInit {
  group: GroupDetailDto | null = null;
  cycles: ExpenseCycleSummaryDto[] = [];
  loading = true;
  error = '';
  leavingGroup = false;
  regeneratingCode = false;
  cycleColumns = ['name', 'period', 'memberCount', 'totalAmount', 'status', 'actions'];

  currentUserId: number | null = null;

  get canShare(): boolean {
    return typeof navigator.share === 'function';
  }

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private groupService: GroupService,
    private cycleService: ExpenseCycleService,
    private auth: AuthService,
    private dialog: MatDialog,
    private dialogService: DialogService,
    private snackBar: MatSnackBar,
    private cdr: ChangeDetectorRef
  ) {}

  ngOnInit(): void {
    this.currentUserId = this.auth.getUserId();
    const id = Number(this.route.snapshot.paramMap.get('id'));
    this.loadAll(id);
  }

  loadAll(id: number): void {
    this.loading = true;
    forkJoin({
      group: this.groupService.getGroup(id),
      cycles: this.cycleService.getAll(id)
    }).subscribe({
      next: ({ group, cycles }) => {
        this.group  = group;
        this.cycles = cycles;
        this.loading = false;
        this.cdr.detectChanges();
      },
      error: () => {
        this.error = 'Group not found or access denied.';
        this.loading = false;
        this.cdr.detectChanges();
      }
    });
  }

  get acceptedMembers(): GroupMemberDto[] {
    return this.group?.members.filter(m => m.status === 'Accepted') ?? [];
  }

  get isCurrentUserMember(): boolean {
    return this.group?.members.some(m => m.userId === this.currentUserId && m.status === 'Accepted') ?? false;
  }

  openEditGroup(): void {
    if (!this.group) return;
    const ref = this.dialog.open(CreateGroupDialogComponent, {
      width: '480px',
      disableClose: true,
      data: { group: this.group }
    });
    ref.afterClosed().subscribe(saved => {
      if (saved) this.loadAll(this.group!.id);
    });
  }

  openManageMembers(): void {
    if (!this.group) return;
    const ref = this.dialog.open(GroupMembersDialogComponent, {
      width: '680px',
      data: { groupId: this.group.id, groupName: this.group.name, isReadOnly: false }
    });
    ref.afterClosed().subscribe(() => this.loadAll(this.group!.id));
  }

  openViewMembers(): void {
    if (!this.group) return;
    this.dialog.open(GroupMembersDialogComponent, {
      width: '680px',
      data: { groupId: this.group.id, groupName: this.group.name, isReadOnly: true }
    });
  }

  openNewCycle(): void {
    if (!this.group) return;
    const ref = this.dialog.open(CreateCycleDialogComponent, {
      width: '520px',
      maxHeight: '90vh',
      disableClose: true,
      data: { groupId: this.group.id, groupMembers: this.acceptedMembers }
    });
    ref.afterClosed().subscribe(created => {
      if (created) this.loadAll(this.group!.id);
    });
  }

  viewCycle(cycleId: number): void {
    this.router.navigate(['/cycles', cycleId]);
  }

  leaveGroup(): void {
    if (!this.group) return;
    this.dialogService.confirm({
      title: 'Leave Group',
      message: `Are you sure you want to leave <strong>${this.group.name}</strong>? All remaining members will be notified.`,
      confirmText: 'Leave',
      cancelText: 'Cancel',
      confirmColor: 'warn'
    }).subscribe(confirmed => {
      if (!confirmed) return;
      this.leavingGroup = true;
      this.groupService.leaveGroup(this.group!.id).subscribe({
        next: () => {
          this.snackBar.open(`You have left ${this.group!.name}.`, 'OK', { duration: 4000 });
          this.router.navigate(['/groups']);
        },
        error: err => {
          this.leavingGroup = false;
          this.snackBar.open(err.error?.message || 'Failed to leave group.', 'OK', { duration: 5000 });
          this.cdr.detectChanges();
        }
      });
    });
  }

  copyJoinCode(): void {
    if (!this.group?.joinCode) return;
    navigator.clipboard.writeText(this.group.joinCode).then(() => {
      this.snackBar.open('Join code copied to clipboard.', 'OK', { duration: 3000 });
    });
  }

  shareJoinCode(): void {
    if (!this.group?.joinCode) return;
    navigator.share({
      title: `Join ${this.group.name}`,
      text: `Use this code to join the group "${this.group.name}": ${this.group.joinCode}`
    }).catch(() => {});
  }

  regenerateCode(): void {
    if (!this.group) return;
    this.dialogService.confirm({
      title: 'Regenerate Join Code',
      message: 'This will invalidate the current code. Anyone with the old code will no longer be able to use it. Continue?',
      confirmText: 'Regenerate',
      cancelText: 'Cancel',
      confirmColor: 'warn'
    }).subscribe(confirmed => {
      if (!confirmed) return;
      this.regeneratingCode = true;
      this.groupService.regenerateJoinCode(this.group!.id).subscribe({
        next: res => {
          this.group = { ...this.group!, joinCode: res.joinCode };
          this.regeneratingCode = false;
          this.snackBar.open('Join code regenerated.', 'OK', { duration: 3000 });
          this.cdr.detectChanges();
        },
        error: err => {
          this.regeneratingCode = false;
          this.snackBar.open(err.error?.message || 'Failed to regenerate code.', 'OK', { duration: 5000 });
          this.cdr.detectChanges();
        }
      });
    });
  }

  back(): void { this.router.navigate(['/groups']); }

  statusClass(status: string): string {
    switch (status?.toLowerCase()) {
      case 'active': return 'status-active';
      case 'closed': return 'status-closed';
      default:       return 'status-draft';
    }
  }
}
