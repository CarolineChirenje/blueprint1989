import { ChangeDetectorRef, Component, OnInit, OnDestroy } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { MatDialog } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';
import { forkJoin, Subscription } from 'rxjs';
import { filter } from 'rxjs/operators';
import { GroupService } from '../../../core/services/group.service';
import { DialogService } from '../../../shared/services/dialog.service';
import { PushMessageService } from '../../../core/services/push-message.service';
import { GroupDto, MyJoinRequestDto } from '../../../shared/models/group.model';
import { CreateGroupDialogComponent } from '../create-group-dialog/create-group-dialog.component';
import { GroupMembersDialogComponent } from '../group-members-dialog/group-members-dialog.component';
import { JoinGroupDialogComponent } from '../join-group-dialog/join-group-dialog.component';

@Component({
  selector: 'app-group-list',
  templateUrl: './group-list.component.html',
  styleUrls: ['./group-list.component.css', '../../../shared/styles/table.css'],
  standalone: false
})
export class GroupListComponent implements OnInit, OnDestroy {
  groups: GroupDto[] = [];
  pendingJoinRequests: MyJoinRequestDto[] = [];
  tableData: (GroupDto | (MyJoinRequestDto & { _isPendingRequest: true }))[] = [];
  errorMessage = '';
  displayedColumns = ['name', 'memberCount', 'status', 'createdAt', 'actions'];
  private pushSub?: Subscription;

  constructor(
    private groupService: GroupService,
    private dialog: MatDialog,
    private dialogService: DialogService,
    private router: Router,
    private route: ActivatedRoute,
    private cdr: ChangeDetectorRef,
    private snackBar: MatSnackBar,
    private pushMessage: PushMessageService
  ) {}

  ngOnInit(): void {
    this.loadGroups();
    // Auto-open join dialog if ?join=CODE is present (from QR scan)
    const joinCode = this.route.snapshot.queryParamMap.get('join');
    if (joinCode) {
      this.router.navigate([], { queryParams: {}, replaceUrl: true });
      this.openJoinDialog(joinCode);
    }
    // Auto-reload when a join request is approved or declined via push
    this.pushSub = this.pushMessage.pushReceived$.pipe(
      filter(e => e.notificationType === 34 || e.notificationType === 35)
    ).subscribe(() => this.loadGroups());
  }

  ngOnDestroy(): void {
    this.pushSub?.unsubscribe();
  }

  loadGroups(): void {
    this.errorMessage = '';
    forkJoin({
      groups: this.groupService.getGroups(),
      pendingRequests: this.groupService.getMyJoinRequests()
    }).subscribe({
      next: ({ groups, pendingRequests }) => {
        this.groups = groups;
        this.pendingJoinRequests = pendingRequests;
        this.tableData = [
          ...groups,
          ...pendingRequests.map(r => ({ ...r, _isPendingRequest: true as const }))
        ];
        this.cdr.detectChanges();
      },
      error: () => { this.errorMessage = 'Failed to load groups.'; this.cdr.detectChanges(); }
    });
  }

  isPendingRequest(row: any): boolean {
    return !!row._isPendingRequest;
  }

  cancelJoinRequest(event: Event, row: any): void {
    event.stopPropagation();
    this.groupService.cancelJoinRequest(row.groupId).subscribe({
      next: () => {
        this.snackBar.open('Join request cancelled.', 'OK', { duration: 3000 });
        this.loadGroups();
      },
      error: err => {
        this.snackBar.open(err.error?.message || 'Failed to cancel request.', 'OK', { duration: 5000 });
      }
    });
  }

  openGroup(group: GroupDto): void {
    this.router.navigate(['/groups', group.id]);
  }

  openCreateDialog(): void {
    const ref = this.dialog.open(CreateGroupDialogComponent, { width: '480px', disableClose: true });
    ref.afterClosed().subscribe(created => { if (created) this.loadGroups(); });
  }

  openJoinDialog(prefillCode?: string): void {
    const ref = this.dialog.open(JoinGroupDialogComponent, {
      width: '420px',
      disableClose: true,
      data: prefillCode ? { joinCode: prefillCode } : null
    });
    ref.afterClosed().subscribe(joined => { if (joined) this.loadGroups(); });
  }

  openMembersDialog(event: Event, group: GroupDto): void {
    event.stopPropagation();
    this.dialog.open(GroupMembersDialogComponent, {
      width: '680px',
      data: { groupId: group.id, groupName: group.name, isReadOnly: false }
    });
  }

  editGroup(event: Event, group: GroupDto): void {
    event.stopPropagation();
    const ref = this.dialog.open(CreateGroupDialogComponent, {
      width: '480px',
      disableClose: true,
      data: { group }
    });
    ref.afterClosed().subscribe(saved => { if (saved) this.loadGroups(); });
  }

  deleteGroup(event: Event, group: GroupDto): void {
    event.stopPropagation();
    this.dialogService.confirmDelete(group.name, `Delete group "${group.name}"? This cannot be undone.`).subscribe(confirmed => {
      if (!confirmed) return;
      this.groupService.deleteGroup(group.id).subscribe({
        next: () => this.loadGroups(),
        error: () => { this.errorMessage = 'Failed to delete group.'; }
      });
    });
  }
}
