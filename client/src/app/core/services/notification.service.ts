import { Injectable } from '@angular/core';
import { HttpClient, HttpHeaders, HttpParams } from '@angular/common/http';
import { BehaviorSubject, Observable } from 'rxjs';
import { tap } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import { AuthService } from './auth.service';
import { NotificationType } from './push-notification.service';

export interface NotificationDto {
  id: number;
  message: string;
  isRead: boolean;
  createdAt: string;
  type: NotificationType;
  deepLinkUrl: string | null;
  relatedEntityId: number | null;
  sentViaPush: boolean;
  isArchived: boolean;
}

export interface NotificationSummaryDto {
  unreadCount: number;
  notifications: NotificationDto[];
}

export interface NotificationLoadOptions {
  unreadOnly?: boolean;
  archivedOnly?: boolean;
  take?: number;
  skip?: number;
}

@Injectable({ providedIn: 'root' })
export class NotificationService {
  private apiUrl = `${environment.apiUrl}/notification`;

  private _unreadCount = new BehaviorSubject<number>(0);
  readonly unreadCount$ = this._unreadCount.asObservable();

  private _notifications = new BehaviorSubject<NotificationDto[]>([]);
  readonly notifications$ = this._notifications.asObservable();

  private lastLoadOptions: NotificationLoadOptions = {};

  constructor(private http: HttpClient, private auth: AuthService) {}

  private headers(): HttpHeaders {
    return new HttpHeaders({ Authorization: `Bearer ${this.auth.getToken()}` });
  }

  private buildParams(options: NotificationLoadOptions): HttpParams {
    let params = new HttpParams();

    if (options.unreadOnly) {
      params = params.set('unreadOnly', 'true');
    }

    if (options.archivedOnly) {
      params = params.set('archivedOnly', 'true');
    }

    if (options.take != null && options.take > 0) {
      params = params.set('take', `${options.take}`);
    }

    if (options.skip != null && options.skip > 0) {
      params = params.set('skip', `${options.skip}`);
    }

    return params;
  }

  fetchSummary(options: NotificationLoadOptions = {}): Observable<NotificationSummaryDto> {
    this.lastLoadOptions = { ...options };

    return this.http.get<NotificationSummaryDto>(this.apiUrl, {
      headers: this.headers(),
      params: this.buildParams(options)
    });
  }

  load(options: NotificationLoadOptions = {}): Observable<NotificationSummaryDto> {
    this.lastLoadOptions = { ...options };

    return this.fetchSummary(options).pipe(
      tap(summary => {
        this._unreadCount.next(summary.unreadCount);
        this._notifications.next(summary.notifications);
      })
    );
  }

  markRead(id: number): Observable<void> {
    return this.http.put<void>(`${this.apiUrl}/${id}/read`, {}, { headers: this.headers() }).pipe(
      tap(() => {
        const current = this._notifications.value;
        const target = current.find(n => n.id === id);
        const wasUnread = !!target && !target.isRead;

        let updated = current.map(n =>
          n.id === id ? { ...n, isRead: true } : n
        );

        if (this.lastLoadOptions.unreadOnly) {
          updated = updated.filter(n => !n.isRead);
        }

        this._notifications.next(updated);

        if (wasUnread) {
          this._unreadCount.next(Math.max(0, this._unreadCount.value - 1));
        }
      })
    );
  }

  markReadMany(ids: number[]): Observable<void> {
    const normalizedIds = [...new Set(ids.filter(id => id > 0))];

    return this.http.put<void>(`${this.apiUrl}/read-batch`, {
      notificationIds: normalizedIds
    }, { headers: this.headers() }).pipe(
      tap(() => {
        const idSet = new Set(normalizedIds);
        const unreadRemoved = this._notifications.value.filter(n => idSet.has(n.id) && !n.isRead).length;

        let updated = this._notifications.value.map(n =>
          idSet.has(n.id) ? { ...n, isRead: true } : n
        );

        if (this.lastLoadOptions.unreadOnly) {
          updated = updated.filter(n => !n.isRead);
        }

        this._notifications.next(updated);

        if (unreadRemoved > 0) {
          this._unreadCount.next(Math.max(0, this._unreadCount.value - unreadRemoved));
        }
      })
    );
  }

  markAllRead(): Observable<void> {
    return this.http.put<void>(`${this.apiUrl}/read-all`, {}, { headers: this.headers() }).pipe(
      tap(() => {
        const updated = this.lastLoadOptions.unreadOnly
          ? []
          : this._notifications.value.map(n => ({ ...n, isRead: true }));

        this._notifications.next(updated);
        this._unreadCount.next(0);
      })
    );
  }

  archiveRead(): Observable<void> {
    return this.http.put<void>(`${this.apiUrl}/archive-read`, {}, { headers: this.headers() }).pipe(
      tap(() => {
        const updated = this.lastLoadOptions.archivedOnly
          ? this._notifications.value
          : this._notifications.value.filter(n => !n.isRead);

        this._notifications.next(updated);
      })
    );
  }

  restore(id: number): Observable<void> {
    return this.http.put<void>(`${this.apiUrl}/${id}/restore`, {}, { headers: this.headers() }).pipe(
      tap(() => {
        let updated = this._notifications.value.map(n =>
          n.id === id ? { ...n, isArchived: false } : n
        );

        if (this.lastLoadOptions.archivedOnly) {
          updated = updated.filter(n => n.isArchived);
        }

        this._notifications.next(updated);
      })
    );
  }

  archiveSingle(id: number): Observable<void> {
    return this.http.put<void>(`${this.apiUrl}/${id}/archive`, {}, { headers: this.headers() }).pipe(
      tap(() => {
        const target = this._notifications.value.find(n => n.id === id);
        const wasUnread = !!target && !target.isRead;

        const updated = this._notifications.value.filter(n => n.id !== id);
        this._notifications.next(updated);

        if (wasUnread) {
          this._unreadCount.next(Math.max(0, this._unreadCount.value - 1));
        }
      })
    );
  }

  markUnread(id: number): Observable<void> {
    return this.http.put<void>(`${this.apiUrl}/${id}/unread`, {}, { headers: this.headers() }).pipe(
      tap(() => {
        const updated = this._notifications.value.map(n =>
          n.id === id ? { ...n, isRead: false } : n
        );
        this._notifications.next(updated);
        this._unreadCount.next(this._unreadCount.value + 1);
      })
    );
  }

  deleteNotification(id: number): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${id}`, { headers: this.headers() }).pipe(
      tap(() => {
        const target = this._notifications.value.find(n => n.id === id);
        const wasUnread = !!target && !target.isRead && !target.isArchived;

        const updated = this._notifications.value.filter(n => n.id !== id);
        this._notifications.next(updated);

        if (wasUnread) {
          this._unreadCount.next(Math.max(0, this._unreadCount.value - 1));
        }
      })
    );
  }

  deleteAllArchived(): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/archived`, { headers: this.headers() }).pipe(
      tap(() => {
        const updated = this._notifications.value.filter(n => !n.isArchived);
        this._notifications.next(updated);
      })
    );
  }
}
