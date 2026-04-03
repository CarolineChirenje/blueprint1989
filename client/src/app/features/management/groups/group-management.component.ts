import { Component, OnInit } from '@angular/core';
import { MatDialog } from '@angular/material/dialog';
import { GroupService } from '../../../core/services/group.service';
import { DialogService } from '../../../shared/services/dialog.service';
import { GroupDto } from '../../../shared/models/group.model';
import { CreateGroupDialogComponent } from './create-group-dialog/create-group-dialog.component';
import { GroupMembersDialogComponent } from './group-members-dialog/group-members-dialog.component';

@Component({
  selector: 'app-group-management',
  templateUrl: './group-management.component.html',
  styleUrls: ['./group-management.component.css', '../../../shared/styles/table.css'],
  standalone: false
})
export class GroupManagementComponent implements OnInit {
  groups: GroupDto[] = [];
  isLoading = false;
  errorMessage = '';
  displayedColumns = ['name', 'memberCount', 'status', 'createdAt', 'actions'];

  constructor(
    private groupService: GroupService,
    private dialog: MatDialog,
    private dialogService: DialogService
  ) {}

  ngOnInit(): void {
    this.loadGroups();
  }

  loadGroups(): void {
    this.isLoading = true;
    this.errorMessage = '';
    this.groupService.getGroups().subscribe({
      next: groups => { this.groups = groups; this.isLoading = false; },
      error: () => { this.errorMessage = 'Failed to load groups.'; this.isLoading = false; }
    });
  }

  openCreateDialog(): void {
    const ref = this.dialog.open(CreateGroupDialogComponent, { width: '480px', disableClose: true });
    ref.afterClosed().subscribe(created => { if (created) this.loadGroups(); });
  }

  openMembersDialog(group: GroupDto): void {
    this.dialog.open(GroupMembersDialogComponent, {
      width: '680px',
      data: { groupId: group.id, groupName: group.name }
    });
  }

  editGroup(group: GroupDto): void {
    const ref = this.dialog.open(CreateGroupDialogComponent, {
      width: '480px',
      disableClose: true,
      data: { group }
    });
    ref.afterClosed().subscribe(saved => { if (saved) this.loadGroups(); });
  }

  deleteGroup(group: GroupDto): void {
    this.dialogService.confirmDelete(group.name, `Delete group "${group.name}"? This cannot be undone.`).subscribe(confirmed => {
      if (!confirmed) return;
      this.groupService.deleteGroup(group.id).subscribe({
        next: () => this.loadGroups(),
        error: () => { this.errorMessage = 'Failed to delete group.'; }
      });
    });
  }
}
