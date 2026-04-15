import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  AppConfigEntry,
  BulkUpdateAppConfigRequest,
  BulkUpdateAppConfigResponse,
  UpdateAppConfigEntryRequest
} from '../models/app-config.model';

@Injectable({ providedIn: 'root' })
export class AppConfigService {
  private readonly apiUrl = `${environment.apiUrl}/app-config`;

  constructor(private http: HttpClient) {}

  getAll(): Observable<AppConfigEntry[]> {
    return this.http.get<AppConfigEntry[]>(this.apiUrl);
  }

  getByKey(key: string): Observable<AppConfigEntry> {
    return this.http.get<AppConfigEntry>(`${this.apiUrl}/${key}`);
  }

  update(key: string, value: string): Observable<AppConfigEntry> {
    const body: UpdateAppConfigEntryRequest = { key, value };
    return this.http.put<AppConfigEntry>(`${this.apiUrl}/${key}`, body);
  }

  bulkUpdate(updates: UpdateAppConfigEntryRequest[]): Observable<BulkUpdateAppConfigResponse> {
    const body: BulkUpdateAppConfigRequest = { updates };
    return this.http.put<BulkUpdateAppConfigResponse>(`${this.apiUrl}/bulk`, body);
  }
}
