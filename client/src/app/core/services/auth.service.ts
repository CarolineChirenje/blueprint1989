import { HttpClient } from '@angular/common/http';
import { Injectable, Injector } from '@angular/core';
import { BehaviorSubject, Observable, of } from 'rxjs';
import { catchError, tap } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import { Router } from '@angular/router';
import { Role } from '../../shared/models/user.model';
import { PushNotificationService } from './push-notification.service';

export interface SignupPayload {
  email: string;
  firstName: string;
  lastName: string;
  phoneNumber?: string;
  password: string;
  role: Role;
  adminPin?: string;
}

@Injectable({ providedIn: 'root' })
export class AuthService {
  private tokenKey = 'bgl_token';
  private userKey = 'bgl_user';
  private apiUrl = `${environment.apiUrl}/auth`;

  private _pushService?: PushNotificationService;

  constructor(private http: HttpClient, private router: Router, private injector: Injector) {
    this.checkTokenExpiration();
  }

  /** Lazy-resolve PushNotificationService to avoid circular DI. */
  private get pushService(): PushNotificationService {
    if (!this._pushService) {
      this._pushService = this.injector.get(PushNotificationService);
    }
    return this._pushService;
  }

  login(email: string, password: string): Observable<any> {
    return this.http.post<any>(`${this.apiUrl}/login`, { email, password });
  }

  signup(data: SignupPayload): Observable<any> {
    return this.http.post(`${this.apiUrl}/signup`, data);
  }

  skipMfaSetup(userId: number, skipToken: string): Observable<any> {
    return this.http.post(`${this.apiUrl}/skip-mfa-setup`, { userId, skipToken });
  }

  verifyMfa(userId: number, code: string) {
    return this.http.post(`${this.apiUrl}/verify-mfa`, { userId, code });
  }

  setToken(token: string, rememberMe = true) {
    if (rememberMe) {
      localStorage.setItem(this.tokenKey, token);
      sessionStorage.removeItem(this.tokenKey);
    } else {
      sessionStorage.setItem(this.tokenKey, token);
      localStorage.removeItem(this.tokenKey);
    }
  }
  getToken(): string | null {
    return localStorage.getItem(this.tokenKey) || sessionStorage.getItem(this.tokenKey);
  }
  clearToken() {
    localStorage.removeItem(this.tokenKey);
    sessionStorage.removeItem(this.tokenKey);
    localStorage.removeItem(this.userKey);
    sessionStorage.removeItem(this.userKey);
  }

  setUserInfo(user: any, rememberMe = true) {
    if (rememberMe) {
      localStorage.setItem(this.userKey, JSON.stringify(user));
      sessionStorage.removeItem(this.userKey);
    } else {
      sessionStorage.setItem(this.userKey, JSON.stringify(user));
      localStorage.removeItem(this.userKey);
    }
  }

  getUserInfo(): any {
    const userStr = localStorage.getItem(this.userKey) || sessionStorage.getItem(this.userKey);
    return userStr ? JSON.parse(userStr) : null;
  }
  
  getUserDisplayName(): string {
    const user = this.getUserInfo();
    if (user && user.firstName && user.lastName) {
      return `${user.firstName} ${user.lastName}`;
    }
    return user?.email || 'User';
  }
  
  getUserId(): number | null {
    const user = this.getUserInfo();
    return user?.id || null;
  }

  getTermId(): number | null {
    const token = this.getToken();
    if (!token) return null;

    try {
      const payload = JSON.parse(atob(token.split('.')[1]));
      return payload.termId ? parseInt(payload.termId, 10) : null;
    } catch (e) {
      return null;
    }
  }

  getTermName(): string | null {
    const token = this.getToken();
    if (!token) return null;

    try {
      const payload = JSON.parse(atob(token.split('.')[1]));
      return payload.termName || null;
    } catch (e) {
      return null;
    }
  }

  getUserRole(): string | null {
    const token = this.getToken();
    if (!token) return null;

    try {
      const payload = JSON.parse(atob(token.split('.')[1]));
      return payload.role || payload['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'] || null;
    } catch (e) {
      return null;
    }
  }

  getUserRoleId(): number | null {
    const token = this.getToken();
    if (!token) return null;

    try {
      const payload = JSON.parse(atob(token.split('.')[1]));
      const role = payload.role;

      if (typeof role === 'string') {
        switch (role) {
          case 'SuperAdmin': return Role.SuperAdmin;
          case 'Admin': return Role.Admin;
          case 'User': return Role.User;
          default: return null;
        }
      }

      return typeof payload.roleId === 'number' ? payload.roleId : null;
    } catch (e) {
      return null;
    }
  }

  isAdmin(): boolean {
    return this.getUserRoleId() === Role.Admin;
  }

  isSuperAdmin(): boolean {
    return this.getUserRoleId() === Role.SuperAdmin;
  }

  isAdminOrAbove(): boolean {
    const roleId = this.getUserRoleId();
    return roleId === Role.SuperAdmin || roleId === Role.Admin;
  }

  // Password Reset Methods
  forgotPassword(email: string): Observable<any> {
    return this.http.post<any>(`${this.apiUrl}/forgot-password`, { email });
  }

  validateResetToken(token: string): Observable<any> {
    return this.http.get<any>(`${this.apiUrl}/reset-password/validate-token`, {
      params: { token }
    }).pipe(
      catchError((error) => {
        console.error('Token validation error:', error);
        return of({ valid: false });
      })
    );
  }

  resetPassword(token: string, newPassword: string): Observable<any> {
    return this.http.post<any>(`${this.apiUrl}/reset-password`, {
      token,
      newPassword
    }).pipe(
      catchError((error) => {
        console.error('Reset password error:', error);
        return of({ success: false, error: 'Failed to reset password' });
      })
    );
  }

  // Email Verification Methods
  verifyEmail(token: string): Observable<any> {
    return this.http.get<any>(`${this.apiUrl}/verify-email`, { params: { token } }).pipe(
      catchError((error) => of({ success: false, error: error?.error?.error ?? 'Verification failed' }))
    );
  }

  resendVerificationEmail(email: string): Observable<any> {
    return this.http.post<any>(`${this.apiUrl}/resend-verification-email`, { email });
  }

  isTokenExpired(): boolean {
    const token = this.getToken();
    if (!token) return true;

    try {
      const payload = JSON.parse(atob(token.split('.')[1]));
      const expiry = payload.exp * 1000; // Convert to milliseconds
      return Date.now() >= expiry;
    } catch (e) {
      return true;
    }
  }

  isTourCompleted(): boolean {
    const user = this.getUserInfo();
    return user?.tourCompleted === true;
  }

  markTourCompletedLocally(): void {
    const user = this.getUserInfo();
    if (user) {
      user.tourCompleted = true;
      // Persist to whichever storage is active
      if (localStorage.getItem(this.userKey)) {
        localStorage.setItem(this.userKey, JSON.stringify(user));
      } else {
        sessionStorage.setItem(this.userKey, JSON.stringify(user));
      }
    }
  }

  checkTokenExpiration(): void {
    // Only clear the stale token — do NOT navigate here.
    // Calling router.navigate() during service construction fires before Angular
    // resolves the initial URL (e.g. /reset-password?token=...), causing the
    // redirect to /login to win the race and swallow the intended route.
    // AuthGuard handles redirect-to-login for protected routes,
    // and the 401 interceptor handles it for expired tokens on API calls.
    if (this.isTokenExpired()) {
      this.clearToken();
    }
  }

  logout(explicitSignOut = false): void {
    // Unsubscribe from push before clearing the token (needs the JWT for auth).
    this.pushService.unsubscribeFromServer().catch(() => {});
    this.clearToken();
    // Only remove the biometric hint on an explicit user-initiated sign-out.
    // For session expiry the hint is preserved so the biometric button reappears.
    if (explicitSignOut) {
      localStorage.removeItem('bgl_biometric_email');
    }
    this.router.navigate(['/login']);
  }
}
