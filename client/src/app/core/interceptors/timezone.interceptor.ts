import { Injectable } from '@angular/core';
import { HttpInterceptor, HttpRequest, HttpHandler, HttpEvent } from '@angular/common/http';
import { Observable } from 'rxjs';

@Injectable()
export class TimezoneInterceptor implements HttpInterceptor {
  intercept(req: HttpRequest<any>, next: HttpHandler): Observable<HttpEvent<any>> {
    const timeZone = Intl.DateTimeFormat().resolvedOptions().timeZone;
    const cloned = req.clone({
      headers: req.headers.set('X-Timezone', timeZone)
    });
    return next.handle(cloned);
  }
}
