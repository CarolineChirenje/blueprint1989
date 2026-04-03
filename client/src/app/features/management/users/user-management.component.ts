import { Component, OnInit } from '@angular/core';
import { MatDialog } from '@angular/material/dialog';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../../environments/environment';
import { UserManagementDto } from '../../../shared/models/user.model';
import { UserGroupRolesDialogComponent } from './user-group-roles-dialog/user-group-roles-dialog.component';

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

  constructor(
    private http: HttpClient,
    private dialog: MatDialog
  ) {}

  ngOnInit(): void {
    this.loadUsers();
  }

  loadUsers(): void {
    this.isLoading = true;
    this.errorMessage = '';
    this.http.get<UserManagementDto[]>(`${environment.apiUrl}/auth/users`).subscribe({
      next: users => { this.users = users; this.isLoading = false; },
      error: () => { this.errorMessage = 'Failed to load users.'; this.isLoading = false; }
    });
  }

  openGroupRolesDialog(user: UserManagementDto): void {
    this.dialog.open(UserGroupRolesDialogComponent, {
      width: '560px',
      data: { userId: user.id, userName: user.fullName }
    });
  }
}
