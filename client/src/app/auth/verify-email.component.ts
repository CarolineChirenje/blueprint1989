import { Component, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { AuthService } from '../core/services/auth.service';

@Component({
  selector: 'app-verify-email',
  templateUrl: './verify-email.component.html',
  styleUrls: ['./verify-email.component.css'],
  standalone: false
})
export class VerifyEmailComponent implements OnInit {
  state: 'verifying' | 'success' | 'error' = 'verifying';
  errorMessage = '';

  resendEmail = '';
  resendState: 'idle' | 'sending' | 'sent' = 'idle';
  resendMessage = '';

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private auth: AuthService
  ) {}

  ngOnInit() {
    const token = this.route.snapshot.queryParamMap.get('token');

    if (!token) {
      this.state = 'error';
      this.errorMessage = 'No verification token was found in the link. Please check your email and try again.';
      return;
    }

    this.auth.verifyEmail(token).subscribe((res: any) => {
      if (res?.success) {
        this.state = 'success';
      } else {
        this.state = 'error';
        this.errorMessage = res?.error ?? 'Verification failed. The link may have expired.';
      }
    });
  }

  goToLogin() {
    this.router.navigate(['/login']);
  }

  resendVerification() {
    if (this.resendState === 'sending' || !this.resendEmail.trim()) return;
    this.resendState = 'sending';
    this.resendMessage = '';
    this.auth.resendVerificationEmail(this.resendEmail.trim()).subscribe({
      next: (r: any) => {
        this.resendState = 'sent';
        this.resendMessage = r?.message ?? 'If your account exists and is unverified, a new link has been sent.';
      },
      error: () => {
        this.resendState = 'idle';
        this.resendMessage = 'Could not send verification email. Please try again.';
      }
    });
  }
}
