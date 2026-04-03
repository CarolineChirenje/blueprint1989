import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  ExpenseCycleDto,
  ExpenseCycleSummaryDto,
  CycleBalanceDto,
  CycleContributionSummaryDto
} from '../../shared/models/expense-cycle.model';

@Injectable({ providedIn: 'root' })
export class ExpenseCycleService {
  private url = `${environment.apiUrl}/cycles`;

  constructor(private http: HttpClient) {}

  getAll(groupId?: number | null): Observable<ExpenseCycleSummaryDto[]> {
    const params = groupId ? new HttpParams().set('groupId', groupId) : undefined;
    return this.http.get<ExpenseCycleSummaryDto[]>(this.url, { params });
  }

  getById(id: number): Observable<ExpenseCycleDto> {
    return this.http.get<ExpenseCycleDto>(`${this.url}/${id}`);
  }

  getBalance(id: number): Observable<CycleBalanceDto> {
    return this.http.get<CycleBalanceDto>(`${this.url}/${id}/balance`);
  }

  create(payload: { name: string; startDate: string; endDate: string; memberUserIds: number[]; groupId?: number | null }): Observable<ExpenseCycleDto> {
    return this.http.post<ExpenseCycleDto>(this.url, payload);
  }

  update(id: number, payload: { name: string; startDate: string; endDate: string }): Observable<ExpenseCycleDto> {
    return this.http.put<ExpenseCycleDto>(`${this.url}/${id}`, payload);
  }

  start(id: number): Observable<ExpenseCycleDto> {
    return this.http.post<ExpenseCycleDto>(`${this.url}/${id}/start`, {});
  }

  close(id: number): Observable<void> {
    return this.http.post<void>(`${this.url}/${id}/close`, {});
  }

  sendReminder(id: number): Observable<void> {
    return this.http.post<void>(`${this.url}/${id}/send-reminder`, {});
  }

  getContributionSummary(id: number): Observable<CycleContributionSummaryDto> {
    return this.http.get<CycleContributionSummaryDto>(`${this.url}/${id}/contribution-summary`);
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.url}/${id}`);
  }

  addMember(cycleId: number, userId: number): Observable<void> {
    return this.http.post<void>(`${this.url}/${cycleId}/members/${userId}`, {});
  }

  removeMember(cycleId: number, userId: number): Observable<void> {
    return this.http.delete<void>(`${this.url}/${cycleId}/members/${userId}`);
  }
}
