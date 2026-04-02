import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

@Injectable({ providedIn: 'root' })
export class SystemService {
  private readonly apiUrl = `${environment.apiUrl}/system`;

  constructor(private http: HttpClient) {}

  restart(): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${this.apiUrl}/restart`, {});
  }
}
