import { Injectable } from '@angular/core';
import { CanActivate, Router } from '@angular/router';
import { AuthService } from '../services/auth.service';

@Injectable({
  providedIn: 'root'
})
export class AuthGuard implements CanActivate {
  constructor(
    private authService: AuthService,
    private router: Router
  ) {}

  canActivate(): boolean {
    const hasToken = !!this.authService.getToken();
    
    if (!hasToken) {
      // No token, redirect to login
      this.router.navigate(['/login']);
      return false;
    }
    
    // Check if token is expired
    if (this.authService.isTokenExpired()) {
      // Token expired, clear it and redirect to login
      this.authService.clearToken();
      this.router.navigate(['/login']);
      return false;
    }
    
    // Has valid token, allow access
    return true;
  }
}
