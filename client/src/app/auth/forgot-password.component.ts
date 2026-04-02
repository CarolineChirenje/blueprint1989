import { Component, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { AuthService } from '../core/services/auth.service';

@Component({
  selector: 'app-forgot-password',
  templateUrl: './forgot-password.component.html',
  styleUrls: ['./forgot-password.component.css'],
  standalone: false
})
export class ForgotPasswordComponent implements OnInit {
  email: string = '';
  loading: boolean = false;
  submitted: boolean = false;
  successMessage: string = '';
  errorMessage: string = '';
  showPassword = false;

  constructor(
    private authService: AuthService,
    private router: Router
  ) { }

  ngOnInit() {
    this.email = '';
    this.loading = false;
    this.submitted = false;
    this.successMessage = '';
    this.errorMessage = '';
  }

  onSubmit() {
    if (!this.email || !this.isValidEmail(this.email)) {
      this.errorMessage = 'Please enter a valid email address';
      return;
    }

    this.loading = true;
    this.errorMessage = '';
    this.successMessage = '';

    this.authService.forgotPassword(this.email).subscribe(
      (response: any) => {
        this.loading = false;
        this.submitted = true;
        this.successMessage = response.message || 'Check your email for password reset link';
        this.email = '';
      },
      (error: any) => {
        this.loading = false;
        this.errorMessage = error?.error?.message || 'An error occurred. Please try again.';
      }
    );
  }

  isValidEmail(email: string): boolean {
    const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
    return emailRegex.test(email);
  }

  goBackToLogin() {
    this.router.navigate(['/login']);
  }
}
