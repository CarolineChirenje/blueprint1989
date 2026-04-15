import { ChangeDetectorRef, Component, OnInit } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Router, ActivatedRoute } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../environments/environment';
import { AuthService } from '../core/services/auth.service';

@Component({
    selector: 'app-change-expired-password',
    templateUrl: './change-expired-password.component.html',
    styleUrls: ['./change-expired-password.component.css'],
    standalone: false
})
export class ChangeExpiredPasswordComponent implements OnInit {
  passwordForm: FormGroup;
  errorMessage: string = '';
  userId: number = 0;
  private apiUrl = `${environment.apiUrl}/auth`;

  constructor(
    private fb: FormBuilder,
    private http: HttpClient,
    private router: Router,
    private route: ActivatedRoute,
    private authService: AuthService,
    private cdr: ChangeDetectorRef
  ) {
    this.passwordForm = this.fb.group({
      currentPassword: ['', [Validators.required]],
      newPassword: ['', [
        Validators.required,
        Validators.minLength(8),
        Validators.pattern(/^(?=.*[A-Z])(?=.*[a-z])(?=.*\d).{8,}$/)
      ]],
      confirmPassword: ['', [Validators.required]]
    });
  }

  ngOnInit() {
    // Get userId from route query params
    this.route.queryParams.subscribe(params => {
      this.userId = params['userId'] ? parseInt(params['userId'], 10) : 0;
      if (!this.userId) {
        this.router.navigate(['/login']);
      }
      this.cdr.detectChanges();
    });
  }

  changePassword() {
    if (this.passwordForm.invalid) {
      this.errorMessage = 'Please fill in all fields correctly';
      return;
    }

    const { currentPassword, newPassword, confirmPassword } = this.passwordForm.value;

    if (newPassword !== confirmPassword) {
      this.errorMessage = 'New passwords do not match';
      return;
    }

    const payload = {
      userId: this.userId,
      currentPassword,
      newPassword
    };

    this.http.post<any>(`${this.apiUrl}/change-expired-password`, payload).subscribe({
      next: (response) => {
        if (response.token) {
          // Store token and full user profile so the app knows who is logged in
          this.authService.setToken(response.token);
          this.authService.setUserInfo(response);
          this.router.navigate(['/dashboard']);
        }
      },
      error: (err) => {
        this.errorMessage = err.error || 'Failed to change password';
        this.cdr.detectChanges();
      }
    });
  }
}
