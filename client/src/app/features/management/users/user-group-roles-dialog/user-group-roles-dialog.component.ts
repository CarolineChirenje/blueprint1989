import { ChangeDetectorRef, Component, Inject, OnInit } from '@angular/core';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { HttpClient } from '@angular/common/http';
import { GroupService } from '../../../../core/services/group.service';
import { UserGroupMembershipDto } from '../../../../shared/models/user.model';
import { environment } from '../../../../../environments/environment';

interface DialogData { userId: number; userName: string; }

@Component({
  selector: 'app-user-group-roles-dialog',
  templateUrl: './user-group-roles-dialog.component.html',
  styleUrls: ['./user-group-roles-dialog.component.css', '../../../../shared/styles/table.css'],
  standalone: false
})
export class UserGroupRolesDialogComponent implements OnInit {
  memberships: UserGroupMembershipDto[] = [];
  isLoading = false;
  error = '';
  savingGroupId: number | null = null;

  constructor(
    @Inject(MAT_DIALOG_DATA) public data: DialogData,
    private dialogRef: MatDialogRef<UserGroupRolesDialogComponent>,
    private groupService: GroupService,
    private http: HttpClient,
    private cdr: ChangeDetectorRef
  ) {}

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.isLoading = true;
    this.error = '';
    this.http.get<UserGroupMembershipDto[]>(
      `${environment.apiUrl}/auth/users/${this.data.userId}/group-memberships`
    ).subscribe({
      next: m => { this.memberships = m; this.isLoading = false; this.cdr.detectChanges(); },
      error: () => { this.error = 'Failed to load group memberships.'; this.isLoading = false; this.cdr.detectChanges(); }
    });
  }

  toggleGroupAdmin(m: UserGroupMembershipDto): void {
    const newRole = m.groupRole === 'GroupAdmin' ? 'GroupMember' : 'GroupAdmin';
    this.savingGroupId = m.groupId;
    this.groupService.updateMemberRole(m.groupId, this.data.userId, { groupRole: newRole }).subscribe({
      next: () => {
        m.groupRole = newRole;
        this.savingGroupId = null;
        this.cdr.detectChanges();
      },
      error: err => {
        this.error = err.error?.message || 'Failed to update role.';
        this.savingGroupId = null;
        this.cdr.detectChanges();
      }
    });
  }

  close(): void { this.dialogRef.close(); }
}
