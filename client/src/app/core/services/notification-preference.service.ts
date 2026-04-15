import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  NotificationPreferenceDto,
  UpdateNotificationPreferencesRequest
} from '../../shared/models/notification-preference.model';

@Injectable({ providedIn: 'root' })
export class NotificationPreferenceService {

  private readonly baseUrl = `${environment.apiUrl}/notification-preferences`;
  private readonly adminBaseUrl = `${environment.apiUrl}/admin/notification-preferences`;

  constructor(private http: HttpClient) {}

  /** Get the current user's notification preferences. */
  getPreferences(): Observable<NotificationPreferenceDto[]> {
    return this.http.get<NotificationPreferenceDto[]>(this.baseUrl);
  }

  /** Update the current user's notification preferences (admin-controlled types are ignored server-side). */
  updatePreferences(request: UpdateNotificationPreferencesRequest): Observable<void> {
    return this.http.put<void>(this.baseUrl, request);
  }

  /** (Admin only) Get notification preferences for a specific user. */
  getAdminPreferences(userId: number): Observable<NotificationPreferenceDto[]> {
    return this.http.get<NotificationPreferenceDto[]>(`${this.adminBaseUrl}/${userId}`);
  }

  /** (Admin only) Update notification preferences for a specific user (all types allowed). */
  updateAdminPreferences(userId: number, request: UpdateNotificationPreferencesRequest): Observable<void> {
    return this.http.put<void>(`${this.adminBaseUrl}/${userId}`, request);
  }
}
