import { Injectable } from '@angular/core';
import { CanActivate, ActivatedRouteSnapshot, RouterStateSnapshot, Router } from '@angular/router';
import { AuthService } from '../services/auth.service';
import { Role } from '../../shared/models/user.model';

@Injectable({
  providedIn: 'root'
})
export class RoleGuard implements CanActivate {
  constructor(
    private authService: AuthService,
    private router: Router
  ) {}

  canActivate(
    route: ActivatedRouteSnapshot,
    state: RouterStateSnapshot
  ): boolean {
    // First check if token exists and is valid
    const hasToken = !!this.authService.getToken();
    if (!hasToken || this.authService.isTokenExpired()) {
      this.authService.clearToken();
      this.router.navigate(['/login']);
      return false;
    }
    
    const allowedRoles = route.data['roles'] as string[];
    const userRoleId = this.authService.getUserRoleId();

    if (userRoleId === null) {
      this.router.navigate(['/login']);
      return false;
    }

    // Convert role names to role IDs for comparison
    const allowedRoleIds = allowedRoles.map(roleName => {
      switch (roleName) {
        case 'SuperAdmin': return Role.SuperAdmin;
        case 'Admin': return Role.Admin;
        case 'Member': return Role.Member;
        default: return -1;
      }
    });
    
    if (allowedRoleIds.includes(userRoleId)) {
      return true;
    }

    // If user doesn't have permission, redirect to dashboard
    this.router.navigate(['/dashboard']);
    return false;
  }
}
