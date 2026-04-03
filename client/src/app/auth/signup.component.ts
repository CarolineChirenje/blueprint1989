import { Component, OnInit } from '@angular/core';
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

  readonly Role = Role;

  roleOptions = [
    { value: Role.Admin,  label: 'Admin',  description: 'System administrator', icon: 'admin_panel_settings', color: '#F57C00' },
    { value: Role.Member, label: 'Member', description: 'Group member',     icon: 'person',               color: '#237A49' }
  ];

  constructor(private fb: FormBuilder, private auth: AuthService, private router: Router) {
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
    if (this.signupForm.invalid || this.isSubmitting) return;
    this.isSubmitting = true;
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
        this.signupSuccess = true;
        this.newUserId = res?.userId ?? null;
        this.skipMfaToken = res?.skipMfaToken ?? null;
        this.errorMsg = '';
        this.isSubmitting = false;
      },
      error: (err) => {
        this.errorMsg = err.error?.message || 'Signup failed. Please try again.';
        this.signupSuccess = false;
        this.isSubmitting = false;
      }
    });
  }

  setupMfaNow(): void {
    this.router.navigate(['/login'], { queryParams: { promptMfa: 'true' } });
  }
}