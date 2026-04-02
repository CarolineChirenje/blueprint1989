import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  ExpenseCycleDto,
  ExpenseCycleSummaryDto,
  CycleBalanceDto
} from '../../shared/models/expense-cycle.model';

@Injectable({ providedIn: 'root' })
export class ExpenseCycleService {
  private url = `${environment.apiUrl}/cycles`;

  constructor(private http: HttpClient) {}

  getAll(): Observable<ExpenseCycleSummaryDto[]> {
    return this.http.get<ExpenseCycleSummaryDto[]>(this.url);
  }

  getById(id: number): Observable<ExpenseCycleDto> {
    return this.http.get<ExpenseCycleDto>(`${this.url}/${id}`);
  }

  getBalance(id: number): Observable<CycleBalanceDto> {
    return this.http.get<CycleBalanceDto>(`${this.url}/${id}/balance`);
  }

  create(payload: { name: string; startDate: string; endDate: string; memberUserIds: number[] }): Observable<ExpenseCycleDto> {
    return this.http.post<ExpenseCycleDto>(this.url, payload);
  }

  update(id: number, payload: { name: string; startDate: string; endDate: string }): Observable<ExpenseCycleDto> {
    return this.http.put<ExpenseCycleDto>(`${this.url}/${id}`, payload);
  }

  close(id: number): Observable<void> {
    return this.http.post<void>(`${this.url}/${id}/close`, {});
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
