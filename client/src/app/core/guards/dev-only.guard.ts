import { Injectable } from '@angular/core';
import { CanActivate, Router } from '@angular/router';
import { environment } from '../../../environments/environment';

/** Allows access only when environment.production === false (Development builds). */
@Injectable({ providedIn: 'root' })
export class DevOnlyGuard implements CanActivate {
  constructor(private router: Router) {}

  canActivate(): boolean {
    if (!environment.production) {
      return true;
    }
    this.router.navigate(['/dashboard']);
    return false;
  }
}
