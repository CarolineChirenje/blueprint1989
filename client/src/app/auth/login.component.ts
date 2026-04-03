import { ChangeDetectorRef, Component, OnInit } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Router, ActivatedRoute } from '@angular/router';
import { AuthService } from '../core/services/auth.service';
import { BiometricService } from '../core/services/biometric.service';
import { PushNotificationService } from '../core/services/push-notification.service';

@Component({
    selector: 'app-login',
    templateUrl: './login.component.html',
    styleUrls: ['./login.component.css'],
    standalone: false
})
export class LoginComponent implements OnInit {
  loginForm: FormGroup;
  mfaFormVisible = false;
  mfaForm: FormGroup;
  tempUserId?: number;
  shouldPromptMfa = false;

  // Email verification state
  emailUnverified = false;
  emailForResend = '';
  resendState: 'idle' | 'sending' | 'sent' = 'idle';
  resendMessage = '';

  // Biometric login state
  biometricAvailable = false;
  biometricEmail: string | null = null;
  loginState: 'idle' | 'processing' | 'unlocked' = 'idle';

  /** How long (ms) the unlocked overlay stays visible before navigating. */
  readonly UNLOCK_DISPLAY_MS = 1200;

  constructor(
    private fb: FormBuilder,
    private auth: AuthService,
    private biometric: BiometricService,
    private pushNotifications: PushNotificationService,
    private router: Router,
    private route: ActivatedRoute,
    private cdr: ChangeDetectorRef
  ) {
    this.loginForm = this.fb.group({
      email: ['', [Validators.required, Validators.email]],
      password: ['', Validators.required],
      rememberMe: [true]
    });

    this.mfaForm = this.fb.group({ code: ['', Validators.required] });
  }

  ngOnInit() {
    const hasToken = this.auth.getToken();
    if (hasToken && !this.auth.isTokenExpired()) {
      this.router.navigate(['/dashboard']);
      return;
    }
    if (hasToken && this.auth.isTokenExpired()) {
      this.auth.clearToken();
    }

    this.route.queryParams.subscribe(params => {
      this.shouldPromptMfa = params['promptMfa'] === 'true';
      this.cdr.detectChanges();
    });

    // Check if this device supports biometrics and has a registered credential hint
    this.biometric.isPlatformAuthenticatorAvailable().then(available => {
      this.biometricAvailable = available;
      this.biometricEmail = this.biometric.getBiometricHintEmail();
      // Pre-fill email so the single Login button auto-detects biometric
      if (this.biometricEmail) {
        this.loginForm.patchValue({ email: this.biometricEmail });
      }
      this.updatePasswordValidator();
    });

    // Re-evaluate password requirement whenever email changes
    this.loginForm.get('email')!.valueChanges.subscribe(() => {
      this.updatePasswordValidator();
    });
  }

  /** True when the typed email matches the stored biometric credential. */
  get isBiometricLogin(): boolean {
    return (
      this.biometricAvailable &&
      !!this.biometricEmail &&
      (this.loginForm.get('email')?.value ?? '').toLowerCase() ===
        this.biometricEmail.toLowerCase()
    );
  }

  private updatePasswordValidator() {
    const ctrl = this.loginForm.get('password')!;
    if (this.isBiometricLogin) {
      ctrl.clearValidators();
    } else {
      ctrl.setValidators(Validators.required);
    }
    ctrl.updateValueAndValidity({ emitEvent: false });
  }

errorMessage: string = '';

submit() {
  if (this.isBiometricLogin) {
    this.loginWithBiometric();
    return;
  }

  if (this.loginForm.invalid) return;

  const { email, password, rememberMe } = this.loginForm.value;
  this.errorMessage = '';
  this.loginState = 'processing';

    this.auth.login(email, password).subscribe({
      next: (r: any) => {
        if (r?.emailUnverified) {
          this.loginState = 'idle';
          this.emailUnverified = true;
          this.emailForResend = email;
          this.cdr.detectChanges();
          return;
        }

        if (r?.passwordExpired) {
          this.loginState = 'idle';
          this.cdr.detectChanges();
          this.router.navigate(['/change-expired-password'], {
            queryParams: { userId: r.id }
          });
          return;
        }

        if (r?.isMfaRequired) {
          this.loginState = 'idle';
          this.mfaFormVisible = true;
          this.tempUserId = r.id;
          this.cdr.detectChanges();
          return;
        }

        if (!r?.token) {
          this.loginState = 'idle';
          this.errorMessage = 'Login failed.';
          this.cdr.detectChanges();
          return;
        }

        this.loginState = 'unlocked';
        this.cdr.detectChanges();
        this.completeLogin(r, rememberMe);
      },
      error: (err) => {
        this.loginState = 'idle';
        if (err?.error?.emailUnverified) {
          this.emailUnverified = true;
          this.emailForResend = email;
          this.cdr.detectChanges();
          return;
        }
        this.errorMessage =
          typeof err?.error === 'string'
            ? err.error
            : (err?.error?.message ?? 'Login failed');
        this.cdr.detectChanges();
      }
    });
}

/** Login using platform biometric (fingerprint / Face ID / Windows Hello). */
loginWithBiometric() {
  if (!this.biometricEmail) return;
  this.errorMessage = '';
  this.loginState = 'processing';

  this.biometric.authenticateWithBiometric(this.biometricEmail).subscribe({
    next: (r: any) => {
      if (!r?.token) {
        this.loginState = 'idle';
        this.errorMessage = 'Biometric login failed — no token received.';
        this.cdr.detectChanges();
        return;
      }
      this.loginState = 'unlocked';
      this.cdr.detectChanges();
      this.completeLogin(r);
    },
    error: (err) => {
      this.loginState = 'idle';
      this.errorMessage =
        typeof err?.error === 'string'
          ? err.error
          : (err?.error?.message ?? err?.message ?? 'Biometric login failed. Please use your password.');
      this.cdr.detectChanges();
    }
  });
}

/** Shared post-login logic: store token, navigate to dashboard. */
  private completeLogin(r: any, rememberMe = true) {
    this.auth.setToken(r.token, rememberMe);
    this.auth.setUserInfo({
      id: r.id,
      email: r.email,
      firstName: r.firstName,
      lastName: r.lastName,
      role: r.role,
      isMfaEnabled: r.isMfaEnabled,
      mfaEnabledAt: r.mfaEnabledAt
    }, rememberMe);
    // Auto-subscribe to push notifications on login (silently fails if denied)
    this.pushNotifications.subscribeToServer().catch(() => {});
    setTimeout(() => {
      if (this.shouldPromptMfa) {
        this.router.navigate(['/profile/security']);
      } else {
        this.router.navigate(['/dashboard']);
      }
    }, this.UNLOCK_DISPLAY_MS);
}

  verifyMfa() {
    if (this.mfaForm.invalid || !this.tempUserId) return;
    const code = this.mfaForm.value.code;
    this.auth.verifyMfa(this.tempUserId, code).subscribe(res => {
      const r: any = res;
      this.auth.setToken(r.token || '');
      this.auth.setUserInfo({
        id: r.id,
        email: r.email,
        firstName: r.firstName,
        lastName: r.lastName,
        role: r.role,
        isMfaEnabled: r.isMfaEnabled,
        mfaEnabledAt: r.mfaEnabledAt
      });
      // Auto-subscribe to push notifications on MFA login (silently fails if denied)
      this.pushNotifications.subscribeToServer().catch(() => {});
      this.router.navigate(['/dashboard']);
    }, err => {
      if (err?.status === 403 && err?.error?.emailUnverified) {
        this.mfaFormVisible = false;
        this.emailUnverified = true;
        this.cdr.detectChanges();
        return;
      }
      this.errorMessage = 'MFA verification failed. Please check your code and try again.';
      this.cdr.detectChanges();
    });
  }

  resendVerification() {
    if (this.resendState === 'sending' || !this.emailForResend) return;
    this.resendState = 'sending';
    this.resendMessage = '';
    this.auth.resendVerificationEmail(this.emailForResend).subscribe({
      next: (r: any) => {
        this.resendState = 'sent';
        this.resendMessage = r?.message ?? 'Verification email sent. Please check your inbox.';
        this.cdr.detectChanges();
      },
      error: () => {
        this.resendState = 'idle';
        this.resendMessage = 'Could not send verification email. Please try again.';
        this.cdr.detectChanges();
      }
    });
  }

  dismissEmailUnverified() {
    this.emailUnverified = false;
    this.emailForResend = '';
    this.resendState = 'idle';
    this.resendMessage = '';
  }

  cancelMfa() {
    this.mfaFormVisible = false;
    this.tempUserId = undefined;
    this.mfaForm.reset();
    this.errorMessage = '';
  }
}
