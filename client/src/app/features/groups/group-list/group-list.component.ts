import { ChangeDetectorRef, Component, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { MatDialog } from '@angular/material/dialog';
import { GroupService } from '../../../core/services/group.service';
import { DialogService } from '../../../shared/services/dialog.service';
import { GroupDto } from '../../../shared/models/group.model';
import { CreateGroupDialogComponent } from '../create-group-dialog/create-group-dialog.component';
import { GroupMembersDialogComponent } from '../group-members-dialog/group-members-dialog.component';
import { JoinGroupDialogComponent } from '../join-group-dialog/join-group-dialog.component';

@Component({
  selector: 'app-group-list',
  templateUrl: './group-list.component.html',
  styleUrls: ['./group-list.component.css', '../../../shared/styles/table.css'],
  standalone: false
})
export class GroupListComponent implements OnInit {
  groups: GroupDto[] = [];
  errorMessage = '';
  displayedColumns = ['name', 'memberCount', 'status', 'createdAt', 'actions'];

  constructor(
    private groupService: GroupService,
    private dialog: MatDialog,
    private dialogService: DialogService,
    private router: Router,
    private route: ActivatedRoute,
    private cdr: ChangeDetectorRef
  ) {}

  ngOnInit(): void {
    this.loadGroups();
    // Auto-open join dialog if ?join=CODE is present (from QR scan)
    const joinCode = this.route.snapshot.queryParamMap.get('join');
    if (joinCode) {
      this.router.navigate([], { queryParams: {}, replaceUrl: true });
      this.openJoinDialog(joinCode);
    }
  }

  loadGroups(): void {
    this.errorMessage = '';
    this.groupService.getGroups().subscribe({
      next: groups => { this.groups = groups; this.cdr.detectChanges(); },
      error: () => { this.errorMessage = 'Failed to load groups.'; this.cdr.detectChanges(); }
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
