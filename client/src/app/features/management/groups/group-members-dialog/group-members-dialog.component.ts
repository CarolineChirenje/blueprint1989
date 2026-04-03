import { ChangeDetectorRef, Component, Inject, OnInit } from '@angular/core';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { HttpClient } from '@angular/common/http';
import { GroupService } from '../../../../core/services/group.service';
import { DialogService } from '../../../../shared/services/dialog.service';
import { GroupMemberDto, InviteGroupMemberRequest } from '../../../../shared/models/group.model';
import { environment } from '../../../../../environments/environment';

interface DialogData { groupId: number; groupName: string; }
interface UserOption { id: number; firstName: string; lastName: string; email: string; }

@Component({
  selector: 'app-group-members-dialog',
  templateUrl: './group-members-dialog.component.html',
  styleUrls: ['./group-members-dialog.component.css'],
  standalone: false
})
export class GroupMembersDialogComponent implements OnInit {
  members: GroupMemberDto[] = [];
  users: UserOption[] = [];
  isSendingInvite = false;
  error = '';
  inviteError = '';
  inviteSuccess = '';

  selectedUserId: number | null = null;
  selectedRole: 'GroupAdmin' | 'GroupMember' = 'GroupMember';
  displayedColumns = ['name', 'email', 'role', 'status', 'actions'];

  constructor(
    @Inject(MAT_DIALOG_DATA) public data: DialogData,
    private dialogRef: MatDialogRef<GroupMembersDialogComponent>,
    private groupService: GroupService,
    private dialogService: DialogService,
    private http: HttpClient,
    private cdr: ChangeDetectorRef
  ) {}

  ngOnInit(): void {
    this.loadMembers();
    this.http.get<UserOption[]>(`${environment.apiUrl}/auth/users`).subscribe({
      next: users => this.users = users,
      error: () => {}
    });
  }

  loadMembers(): void {
    this.groupService.getGroupMembers(this.data.groupId).subscribe({
      next: members => { this.members = members; this.cdr.detectChanges(); },
      error: () => { this.error = 'Failed to load members.'; this.cdr.detectChanges(); }
    });
  }

  get acceptedMembers(): GroupMemberDto[] {
    return this.members.filter(m => m.status === 'Accepted');
  }

  get pendingMembers(): GroupMemberDto[] {
    return this.members.filter(m => m.status === 'Pending');
  }

  get availableUsers(): UserOption[] {
    const memberIds = new Set(this.members.map(m => m.userId));
    return this.users.filter(u => !memberIds.has(u.id));
  }

  sendInvite(): void {
    if (!this.selectedUserId) return;
    this.isSendingInvite = true;
    this.inviteError = '';
    this.inviteSuccess = '';

    const req: InviteGroupMemberRequest = {
      userId: this.selectedUserId,
      groupRole: this.selectedRole
    };

    this.groupService.inviteMember(this.data.groupId, req).subscribe({
      next: () => {
        this.inviteSuccess = 'Invite sent.';
        this.selectedUserId = null;
        this.selectedRole = 'GroupMember';
        this.isSendingInvite = false;
        this.loadMembers();
      },
      error: err => {
        this.inviteError = err.error?.message || 'Failed to send invite.';
        this.isSendingInvite = false;
      }
    });
  }

  removeMember(member: GroupMemberDto): void {
    const label = `${member.firstName} ${member.lastName}`.trim();
    const action = member.status === 'Pending' ? 'Cancel invite for' : 'Remove';
    this.dialogService.confirm({
      title: `${action} member`,
      message: `${action} <strong>${label}</strong> from this group?`,
      confirmText: action,
      cancelText: 'Cancel',
      confirmColor: 'warn'
    }).subscribe(confirmed => {
      if (!confirmed) return;
      this.groupService.removeMember(this.data.groupId, member.userId).subscribe({
        next: () => this.loadMembers(),
        error: err => { this.error = err.error?.message || 'Failed to remove member.'; }
      });
    });
  }

  changeRole(member: GroupMemberDto, newRole: 'GroupAdmin' | 'GroupMember'): void {
    this.groupService.updateMemberRole(this.data.groupId, member.userId, { groupRole: newRole }).subscribe({
      next: () => this.loadMembers(),
      error: err => { this.error = err.error?.message || 'Failed to update role.'; }
    });
  }

  close(): void { this.dialogRef.close(); }
}
