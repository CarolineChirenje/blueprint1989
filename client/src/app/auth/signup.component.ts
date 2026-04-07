import { ChangeDetectorRef, Component, OnInit, OnDestroy } from '@angular/core';
import { FormBuilder, FormGroup, Validators, AbstractControl, ValidationErrors } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '../core/services/auth.service';
import { Role } from '../shared/models/user.model';

@Component({
    selector: 'app-signup',
    templateUrl: './signup.component.html',
    styleUrls: ['./signup.component.css'],
    standalone: false
})
export class SignupComponent implements OnInit, OnDestroy {
  signupForm: FormGroup;
  errorMsg: string = '';
  signupSuccess: boolean = false;
  isSubmitting: boolean = false;

  // Email verification flow state
  confirmedEmail: string = '';
  emailSent: boolean = false;
  isSendingEmail: boolean = false;
  sendError: string = '';
  resendCooldown: number = 0;
  private cooldownInterval: ReturnType<typeof setInterval> | null = null;

  readonly Role = Role;

  roleOptions = [
    { value: Role.Admin,  label: 'Admin',  description: 'System administrator', icon: 'admin_panel_settings', color: '#F57C00' },
    { value: Role.Member, label: 'Member', description: 'Group member',     icon: 'person',               color: '#237A49' }
  ];

  constructor(private fb: FormBuilder, private auth: AuthService, private router: Router, private cdr: ChangeDetectorRef) {
    this.signupForm = this.fb.group({
      role:            [null, Validators.required],
      email:           ['', [Validators.required, Validators.email]],
      confirmEmail:    ['', [Validators.required, Validators.email]],
      firstName:       ['', Validators.required],
      lastName:        ['', Validators.required],
      password:        ['', [Validators.required, Validators.minLength(8),
                             Validators.pattern(/^(?=.*[A-Z])(?=.*[a-z])(?=.*\d).{8,}$/)]],
      confirmPassword: ['', Validators.required],
      adminPin:        ['']
    }, { validators: [this.passwordMatchValidator, this.emailMatchValidator] });
  }

  ngOnInit(): void {
    if (this.auth.getToken()) {
      this.router.navigate(['/dashboard']);
    }
    this.signupForm.get('role')!.valueChanges.subscribe(() => {
      this.updateConditionalValidators();
    });
  }

  get selectedRole(): Role | null {
    return this.signupForm.get('role')!.value;
  }

  get isAdmin(): boolean {
    return this.selectedRole === Role.Admin;
  }

  passwordMatchValidator(form: AbstractControl): ValidationErrors | null {
    const pw = form.get('password')?.value;
    const cpw = form.get('confirmPassword')?.value;
    return pw === cpw ? null : { mismatch: true };
  }

  emailMatchValidator(form: AbstractControl): ValidationErrors | null {
    const email = form.get('email')?.value;
    const confirmEmail = form.get('confirmEmail')?.value;
    return email === confirmEmail ? null : { emailMismatch: true };
  }

  updateConditionalValidators(): void {
    const adminPin = this.signupForm.get('adminPin')!;
    adminPin.clearValidators();
    if (this.isAdmin) {
      adminPin.setValidators([Validators.required]);
    }
    adminPin.updateValueAndValidity();
  }

  submit(): void {
    this.signupForm.markAllAsTouched();
    if (this.signupForm.invalid || this.isSubmitting) return;
    this.isSubmitting = true;
    const v = this.signupForm.value;
    this.confirmedEmail = v.email;

    this.auth.signup({
      email:     v.email,
      firstName: v.firstName,
      lastName:  v.lastName,
      password:  v.password,
      role:      v.role,
      adminPin:  this.isAdmin ? v.adminPin : undefined
    }).subscribe({
      next: () => {
        this.signupSuccess = true;
        this.errorMsg = '';
        this.isSubmitting = false;
        this.cdr.detectChanges();
      },
      error: (err) => {
        const msg = err.error?.message || err.message || err.status || JSON.stringify(err);
        this.errorMsg = msg || 'Signup failed. Please try again.';
        this.signupSuccess = false;
        this.isSubmitting = false;
        this.cdr.detectChanges();
      }
    });
  }

  sendVerificationEmail(): void {
    if (this.isSendingEmail) return;
    this.isSendingEmail = true;
    this.sendError = '';

    this.auth.resendVerificationEmail(this.confirmedEmail).subscribe({
      next: () => {
        this.emailSent = true;
        this.isSendingEmail = false;
        this.startResendCooldown();
        this.cdr.detectChanges();
      },
      error: () => {
        this.sendError = 'Failed to send verification email. Please try again.';
        this.isSendingEmail = false;
        this.cdr.detectChanges();
      }
    });
  }

  resendEmail(): void {
    if (this.resendCooldown > 0) return;
    this.sendVerificationEmail();
  }

  goBackToForm(): void {
    this.signupSuccess = false;
    this.emailSent = false;
    this.isSendingEmail = false;
    this.sendError = '';
  }

  goToLogin(): void {
    this.router.navigate(['/login']);
  }

  private startResendCooldown(): void {
    this.clearCooldown();
    this.resendCooldown = 60;
    this.cooldownInterval = setInterval(() => {
      this.resendCooldown--;
      if (this.resendCooldown <= 0) {
        this.clearCooldown();
      }
      this.cdr.detectChanges();
    }, 1000);
  }

  private clearCooldown(): void {
    if (this.cooldownInterval) {
      clearInterval(this.cooldownInterval);
      this.cooldownInterval = null;
    }
    this.resendCooldown = 0;
  }

  ngOnDestroy(): void {
    this.clearCooldown();
  }
}