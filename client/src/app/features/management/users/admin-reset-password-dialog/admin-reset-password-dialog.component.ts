import { ChangeDetectorRef, Component, Inject, OnInit } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../../../environments/environment';
import { UserManagementDto } from '../../../../shared/models/user.model';

export interface AdminResetPasswordDialogData {
  user: UserManagementDto;
}

@Component({
  selector: 'app-admin-reset-password-dialog',
  templateUrl: './admin-reset-password-dialog.component.html',
  styleUrls: ['./admin-reset-password-dialog.component.css'],
  standalone: false
})
export class AdminResetPasswordDialogComponent implements OnInit {
  form!: FormGroup;
  saving = false;
  error = '';

  private passwordPattern = /^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).{8,}$/;

  constructor(
    @Inject(MAT_DIALOG_DATA) public data: AdminResetPasswordDialogData,
    private dialogRef: MatDialogRef<AdminResetPasswordDialogComponent>,
    private fb: FormBuilder,
    private http: HttpClient,
    private cdr: ChangeDetectorRef
  ) {}

  ngOnInit(): void {
    this.form = this.fb.group({
      newPassword: ['', [Validators.required, Validators.pattern(this.passwordPattern)]],
      confirmPassword: ['', [Validators.required]]
    });
  }

  get passwordMismatch(): boolean {
    const { newPassword, confirmPassword } = this.form.value;
    return confirmPassword && newPassword !== confirmPassword;
  }

  save(): void {
    if (this.form.invalid || this.passwordMismatch) return;
    this.saving = true;
    this.error = '';
    this.http.put(
      `${environment.apiUrl}/auth/users/${this.data.user.id}/reset-password`,
      { userId: this.data.user.id, newPassword: this.form.value.newPassword }
    ).subscribe({
      next: () => this.dialogRef.close(true),
      error: err => {
        this.error = err.error?.message || 'Failed to reset password.';
        this.saving = false;
        this.cdr.detectChanges();
      }
    });
  }

  cancel(): void {
    this.dialogRef.close();
  }
}
