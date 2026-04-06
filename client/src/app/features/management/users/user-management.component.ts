import { ChangeDetectorRef, Component, OnInit } from '@angular/core';
import { MatDialog } from '@angular/material/dialog';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../../environments/environment';
import { UserManagementDto } from '../../../shared/models/user.model';
import { UserGroupRolesDialogComponent } from './user-group-roles-dialog/user-group-roles-dialog.component';
import { EditUserDialogComponent } from './edit-user-dialog/edit-user-dialog.component';
import { AdminResetPasswordDialogComponent } from './admin-reset-password-dialog/admin-reset-password-dialog.component';
import { AuthService } from '../../../core/services/auth.service';
import { DialogService } from '../../../shared/services/dialog.service';

@Component({
  selector: 'app-user-management',
  templateUrl: './user-management.component.html',
  styleUrls: ['./user-management.component.css', '../../../shared/styles/table.css'],
  standalone: false
})
export class UserManagementComponent implements OnInit {
  users: UserManagementDto[] = [];
  isLoading = false;
  errorMessage = '';
  displayedColumns = ['fullName', 'email', 'role', 'status', 'actions'];

  get isSuperAdmin(): boolean { return this.authService.isSuperAdmin(); }
  get isAdminOrAbove(): boolean { return this.authService.isAdminOrAbove(); }

  constructor(
    private http: HttpClient,
    private dialog: MatDialog,
    private dialogService: DialogService,
    private authService: AuthService,
    private cdr: ChangeDetectorRef
  ) {}

  ngOnInit(): void {
    this.loadUsers();
  }

  loadUsers(): void {
    this.isLoading = true;
    this.errorMessage = '';
    this.http.get<UserManagementDto[]>(`${environment.apiUrl}/auth/users`).subscribe({
      next: users => { this.users = users; this.isLoading = false; this.cdr.detectChanges(); },
      error: () => { this.errorMessage = 'Failed to load users.'; this.isLoading = false; this.cdr.detectChanges(); }
    });
  }

  openGroupRolesDialog(user: UserManagementDto): void {
    this.dialog.open(UserGroupRolesDialogComponent, {
      width: '560px',
      data: { userId: user.id, userName: user.fullName }
    });
  }

  openEditDialog(user: UserManagementDto): void {
    const ref = this.dialog.open(EditUserDialogComponent, {
      width: '440px',
      data: { user }
    });
    ref.afterClosed().subscribe(result => {
      if (result) {
        const u = this.users.find(x => x.id === user.id);
        if (u) {
          u.firstName = result.firstName;
          u.lastName  = result.lastName;
          u.fullName  = `${result.firstName} ${result.lastName}`;
          u.email     = result.email;
          u.role      = result.role;
          u.roleName  = result.role;
          this.cdr.detectChanges();
        }
      }
    });
  }

  toggleStatus(user: UserManagementDto): void {
    const action = user.isActive ? 'deactivate' : 'activate';
    const label  = action.charAt(0).toUpperCase() + action.slice(1);
    this.dialogService.confirm({
      title:        `${label} User`,
      message:      `Are you sure you want to ${action} ${user.fullName}?`,
      confirmText:  label,
      confirmColor: user.isActive ? 'warn' : 'primary'
    }).subscribe(confirmed => {
      if (!confirmed) return;
      this.http.put(
        `${environment.apiUrl}/auth/users/${user.id}/status`,
        { userId: user.id, isActive: !user.isActive }
      ).subscribe({
        next: () => {
          user.isActive    = !user.isActive;
          user.activeStatus = user.isActive ? 'Active' : 'Inactive';
          this.cdr.detectChanges();
        },
        error: err => {
          this.errorMessage = err.error?.message || 'Failed to update user status.';
          this.cdr.detectChanges();
        }
      });
    });
  }

  deleteUser(user: UserManagementDto): void {
    this.dialogService.confirm({
      title:        'Delete User',
      message:      `Permanently delete ${user.fullName}? This action cannot be undone.`,
      confirmText:  'Delete',
      confirmColor: 'warn'
    }).subscribe(confirmed => {
      if (!confirmed) return;
      this.http.delete(`${environment.apiUrl}/auth/users/${user.id}`).subscribe({
        next: () => {
          this.users = this.users.filter(u => u.id !== user.id);
          this.cdr.detectChanges();
        },
        error: err => {
          this.errorMessage = err.error?.message || 'Failed to delete user.';
          this.cdr.detectChanges();
        }
      });
    });
  }

  verifyEmail(user: UserManagementDto): void {
    this.dialogService.confirm({
      title:        'Verify Email',
      message:      `Manually verify the email for <strong>${user.fullName}</strong> (${user.email})?<br><br>This will allow them to log in without clicking the email verification link.`,
      confirmText:  'Verify',
      confirmColor: 'primary'
    }).subscribe(confirmed => {
      if (!confirmed) return;
      this.http.put(`${environment.apiUrl}/auth/users/${user.id}/verify-email`, {}).subscribe({
        next: () => {
          user.isEmailVerified = true;
          this.cdr.detectChanges();
        },
        error: err => {
          this.errorMessage = err.error?.message || 'Failed to verify email.';
          this.cdr.detectChanges();
        }
      });
    });
  }

  openResetPasswordDialog(user: UserManagementDto): void {
    const ref = this.dialog.open(AdminResetPasswordDialogComponent, {
      width: '440px',
      data: { user }
    });
    ref.afterClosed().subscribe(result => {
      if (result) {
        this.errorMessage = '';
      }
    });
  }
}
