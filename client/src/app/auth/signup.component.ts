import { ChangeDetectorRef, Component, OnInit } from '@angular/core';
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
export class SignupComponent implements OnInit {
  signupForm: FormGroup;
  errorMsg: string = '';
  signupSuccess: boolean = false;
  isSubmitting: boolean = false;
  newUserId: number | null = null;
  skipMfaToken: string | null = null;
  debugLog: string[] = [];

  readonly Role = Role;

  roleOptions = [
    { value: Role.Admin,  label: 'Admin',  description: 'System administrator', icon: 'admin_panel_settings', color: '#F57C00' },
    { value: Role.Member, label: 'Member', description: 'Group member',     icon: 'person',               color: '#237A49' }
  ];

  constructor(private fb: FormBuilder, private auth: AuthService, private router: Router, private cdr: ChangeDetectorRef) {
    this.signupForm = this.fb.group({
      role:            [null, Validators.required],
      email:           ['', [Validators.required, Validators.email]],
      firstName:       ['', Validators.required],
      lastName:        ['', Validators.required],
      password:        ['', [Validators.required, Validators.minLength(8),
                             Validators.pattern(/^(?=.*[A-Z])(?=.*[a-z])(?=.*\d).{8,}$/)]],
      confirmPassword: ['', Validators.required],
      adminPin:        ['']
    }, { validators: this.passwordMatchValidator });
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

  updateConditionalValidators(): void {
    const adminPin = this.signupForm.get('adminPin')!;
    adminPin.clearValidators();
    if (this.isAdmin) {
      adminPin.setValidators([Validators.required]);
    }
    adminPin.updateValueAndValidity();
  }

  submit(): void {
    this.debugLog.unshift('TAP');
    this.cdr.detectChanges();
    this.signupForm.markAllAsTouched();
    if (this.signupForm.invalid || this.isSubmitting) {
      this.debugLog.unshift(`INVALID:${JSON.stringify(Object.keys(this.signupForm.controls).filter(k=>this.signupForm.get(k)?.invalid))}`);
      this.cdr.detectChanges();
      return;
    }
    this.isSubmitting = true;
    this.debugLog.unshift('HTTP_START');
    this.cdr.detectChanges();
    const v = this.signupForm.value;

    this.auth.signup({
      email:     v.email,
      firstName: v.firstName,
      lastName:  v.lastName,
      password:  v.password,
      role:      v.role,
      adminPin:  this.isAdmin ? v.adminPin : undefined
    }).subscribe({
      next: (res) => {
        this.debugLog.unshift('HTTP_OK');
        this.signupSuccess = true;
        this.newUserId = res?.userId ?? null;
        this.skipMfaToken = res?.skipMfaToken ?? null;
        this.errorMsg = '';
        this.isSubmitting = false;
        this.cdr.detectChanges();
      },
      error: (err) => {
        const msg = err.error?.message || err.message || err.status || JSON.stringify(err);
        this.debugLog.unshift(`ERR:${msg}`);
        this.errorMsg = msg || 'Signup failed. Please try again.';
        this.signupSuccess = false;
        this.isSubmitting = false;
        this.cdr.detectChanges();
      }
    });
  }

  setupMfaNow(): void {
    this.router.navigate(['/login'], { queryParams: { promptMfa: 'true' } });
  }

  skipMfa(): void {
    this.router.navigate(['/login']);
  }
}