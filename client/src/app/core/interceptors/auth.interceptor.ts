import { Injectable, Injector } from '@angular/core';
import { HttpInterceptor, HttpRequest, HttpHandler, HttpEvent, HttpErrorResponse } from '@angular/common/http';
import { Observable, throwError } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { AuthService } from '../services/auth.service';
import { DialogService } from '../../shared/services/dialog.service';

@Injectable()
export class AuthInterceptor implements HttpInterceptor {
  // Lazily resolved to avoid the circular dependency:
  //   HttpClient → AuthInterceptor → AuthService → HttpClient
  private _authService?: AuthService;
  private _dialogService?: DialogService;

  constructor(private injector: Injector) {}

  private get authService(): AuthService {
    if (!this._authService) {
      this._authService = this.injector.get(AuthService);
    }
    return this._authService;
  }

  private get dialogService(): DialogService {
    if (!this._dialogService) {
      this._dialogService = this.injector.get(DialogService);
    }
    return this._dialogService;
  }

  intercept(req: HttpRequest<any>, next: HttpHandler): Observable<HttpEvent<any>> {
    const token = this.authService.getToken();
    const isPasswordResetFlow = req.url.includes('/forgot-password') || 
                                 req.url.includes('/reset-password');
    
    // For password reset flows, pass through without adding auth headers
    // and don't redirect to login on errors
    if (isPasswordResetFlow) {
      return next.handle(req);
    }
    
    // For other endpoints, add auth token if available
    if (token) {
      const cloned = req.clone({
        headers: req.headers.set('Authorization', `Bearer ${token}`)
      });
      return next.handle(cloned).pipe(
        catchError((error: HttpErrorResponse) => {
          if (error.status === 401) {
            // Token expired or invalid, logout user
            this.authService.logout();
          } else if (error.status === 403) {
            this.dialogService.permissionDenied('You do not have permission to access this feature.').subscribe();
          }
          return throwError(() => error);
        })
      );
    }
    
    return next.handle(req);
  }
}
