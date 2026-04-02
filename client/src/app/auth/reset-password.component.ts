import { Component, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { AuthService } from '../core/services/auth.service';

@Component({
  selector: 'app-reset-password',
  templateUrl: './reset-password.component.html',
  styleUrls: ['./reset-password.component.css'],
  standalone: false
})
export class ResetPasswordComponent implements OnInit {
  token: string = '';
  email: string = '';
  newPassword: string = '';
  confirmPassword: string = '';
  loading: boolean = false;
  validating: boolean = true;
  tokenValid: boolean = false;
  errorMessage: string = '';
  successMessage: string = '';
  showPassword: boolean = false;
  showConfirmPassword: boolean = false;
  passwordStrength: 'weak' | 'medium' | 'strong' = 'weak';
  submitted: boolean = false;

  constructor(
    private authService: AuthService,
    private route: ActivatedRoute,
    private router: Router
  ) { }

  ngOnInit() {
    this.route.queryParams.subscribe((params) => {
      const token = params['token'];
      if (!token) {
        this.validating = false;
        this.errorMessage = 'No reset token provided';
        return;
      }

      this.token = token;
      this.validateToken();
    });
  }

  validateToken() {
    this.validating = true;
    this.authService.validateResetToken(this.token).subscribe(
      (response: any) => {
        this.validating = false;
        if (response.valid) {
          this.tokenValid = true;
          this.email = response.email || '';
        } else {
          this.errorMessage = 'Invalid or expired password reset token. Please request a new one.';
        }
      },
      (error: any) => {
        this.validating = false;
        this.errorMessage = error?.error?.message || 'Failed to validate token. Please try again.';
      }
    );
  }

  calculatePasswordStrength() {
    if (!this.newPassword) {
      this.passwordStrength = 'weak';
      return;
    }

    let strength = 0;
    const password = this.newPassword;

    // Check length
    if (password.length >= 8) strength++;
    if (password.length >= 12) strength++;
    if (password.length >= 16) strength++;

    // Check character types
    if (/[a-z]/.test(password)) strength++;
    if (/[A-Z]/.test(password)) strength++;
    if (/\d/.test(password)) strength++;
    if (/[^a-zA-Z\d]/.test(password)) strength++;

    if (strength < 3) {
      this.passwordStrength = 'weak';
    } else if (strength < 5) {
      this.passwordStrength = 'medium';
    } else {
      this.passwordStrength = 'strong';
    }
  }

  onPasswordChange() {
    this.calculatePasswordStrength();
  }

  isPasswordValid(): boolean {
    const passwordRegex = /^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).{8,}$/;
    return passwordRegex.test(this.newPassword);
  }

  doPasswordsMatch(): boolean {
    if (!this.newPassword || !this.confirmPassword) {
      return false;
    }
    return this.newPassword === this.confirmPassword;
  }

  canSubmit(): boolean {
    return this.isPasswordValid() && this.doPasswordsMatch() && !this.loading;
  }

  onSubmit() {
    if (!this.canSubmit()) {
      return;
    }

    this.loading = true;
    this.errorMessage = '';
    this.successMessage = '';

    this.authService.resetPassword(this.token, this.newPassword).subscribe(
      (response: any) => {
        this.loading = false;
        if (response.success) {
          this.successMessage = 'Password reset successful! Logging you in...';
          
          // Store token and user info
          if (response.token) {
            this.authService.setToken(response.token);
            this.authService.setUserInfo(response.user);
            this.authService.loadAccessibleConditions();
          }

          // Redirect to dashboard after brief delay
          setTimeout(() => {
            this.router.navigate(['/dashboard']);
          }, 1500);
        } else {
          this.errorMessage = response.error || 'Failed to reset password';
        }
      },
      (error: any) => {
        this.loading = false;
        this.errorMessage = error?.error?.error || error?.error?.message || 'An error occurred. Please try again.';
      }
    );
  }

  goBackToForgotPassword() {
    this.router.navigate(['/forgot-password']);
  }
}
