import { Component, OnInit } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { AuthService } from '../../../core/services/auth.service';
import { environment } from '../../../../environments/environment';

@Component({
    selector: 'app-profile',
    templateUrl: './profile.component.html',
    styleUrls: ['./profile.component.css'],
    standalone: false
})
export class ProfileComponent implements OnInit {
  profileForm: FormGroup;
  errorMessage: string = '';
  successMessage: string = '';
  private apiUrl = `${environment.apiUrl}/auth`;

  constructor(
    private fb: FormBuilder,
    private http: HttpClient,
    public auth: AuthService
  ) {
    this.profileForm = this.fb.group({
      email: [{ value: '', disabled: true }],
      role: [{ value: '', disabled: true }],
      firstName: ['', [Validators.required]],
      lastName: ['', [Validators.required]],
      currentPassword: [''],
      newPassword: ['', [Validators.minLength(8), Validators.pattern(/^(?=.*[A-Z])(?=.*[a-z])(?=.*\d).{8,}$/)]],
      confirmPassword: ['']
    });
  }

  ngOnInit() {
    this.loadUserProfile();
  }

  loadUserProfile() {
    const user = this.auth.getUserInfo();
    const role = this.auth.getUserRole();
    if (user) {
      this.profileForm.patchValue({
        email: user.email,
        role: role || 'Unknown',
        firstName: user.firstName,
        lastName: user.lastName
      });
    }
  }

  updateProfile() {
    if (this.profileForm.invalid) {
      this.errorMessage = 'Please fill in all required fields correctly';
      return;
    }

    const { firstName, lastName, currentPassword, newPassword, confirmPassword } = this.profileForm.value;

    if (newPassword && newPassword !== confirmPassword) {
      this.errorMessage = 'New password and confirmation do not match';
      return;
    }

    if (newPassword && !currentPassword) {
      this.errorMessage = 'Current password is required to change password';
      return;
    }

    const token = this.auth.getToken();
    const headers = new HttpHeaders({
      'Authorization': `Bearer ${token}`,
      'Content-Type': 'application/json'
    });

    const payload: any = { firstName, lastName };
    if (newPassword) {
      payload.currentPassword = currentPassword;
      payload.newPassword = newPassword;
    }

    this.http.put(`${this.apiUrl}/profile`, payload, { headers }).subscribe({
      next: () => {
        this.successMessage = 'Profile updated successfully';
        this.errorMessage = '';
        const user = this.auth.getUserInfo();
        user.firstName = firstName;
        user.lastName = lastName;
        this.auth.setUserInfo(user);
        this.profileForm.patchValue({ currentPassword: '', newPassword: '', confirmPassword: '' });
      },
      error: (err) => {
        this.errorMessage = err.error?.message || 'Failed to update profile';
        this.successMessage = '';
      }
    });
  }
}
