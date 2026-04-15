import { ChangeDetectorRef, Component, Inject, OnInit } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../../../environments/environment';
import { UserManagementDto } from '../../../../shared/models/user.model';
import { AuthService } from '../../../../core/services/auth.service';

export interface EditUserDialogData {
  user: UserManagementDto;
}

interface RoleOption {
  value: string;
  label: string;
}

@Component({
  selector: 'app-edit-user-dialog',
  templateUrl: './edit-user-dialog.component.html',
  styleUrls: ['./edit-user-dialog.component.css'],
  standalone: false
})
export class EditUserDialogComponent implements OnInit {
  form!: FormGroup;
  saving = false;
  error = '';
  roleOptions: RoleOption[];

  constructor(
    @Inject(MAT_DIALOG_DATA) public data: EditUserDialogData,
    private dialogRef: MatDialogRef<EditUserDialogComponent>,
    private fb: FormBuilder,
    private http: HttpClient,
    private authService: AuthService,
    private cdr: ChangeDetectorRef
  ) {
    const isSuperAdmin = this.authService.isSuperAdmin();
    this.roleOptions = isSuperAdmin
      ? [
          { value: 'SuperAdmin', label: 'Super Admin' },
          { value: 'Admin', label: 'Admin' },
          { value: 'Member', label: 'Member' }
        ]
      : [
          { value: 'Admin', label: 'Admin' },
          { value: 'Member', label: 'Member' }
        ];
  }

  ngOnInit(): void {
    this.form = this.fb.group({
      firstName: [this.data.user.firstName, [Validators.required, Validators.maxLength(100)]],
      lastName:  [this.data.user.lastName,  [Validators.required, Validators.maxLength(100)]],
      email:     [this.data.user.email,     [Validators.required, Validators.email, Validators.maxLength(256)]],
      role:      [this.data.user.role,      [Validators.required]]
    });
  }

  save(): void {
    if (this.form.invalid) return;
    this.saving = true;
    this.error = '';
    const { firstName, lastName, email, role } = this.form.value;
    this.http.put(
      `${environment.apiUrl}/auth/users/${this.data.user.id}`,
      { userId: this.data.user.id, firstName, lastName, email, role }
    ).subscribe({
      next: () => this.dialogRef.close({ firstName, lastName, email, role }),
      error: err => {
        this.error = err.error?.message || 'Failed to update user.';
        this.saving = false;
        this.cdr.detectChanges();
      }
    });
  }

  cancel(): void {
    this.dialogRef.close();
  }
}
