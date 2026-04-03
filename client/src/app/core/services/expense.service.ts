import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ExpenseDto, MemberObligationDto, ObligationsSummaryDto } from '../../shared/models/expense-cycle.model';

@Injectable({ providedIn: 'root' })
export class ExpenseService {
  private url = `${environment.apiUrl}/expenses`;
  private dashboardUrl = `${environment.apiUrl}/dashboard`;

  constructor(private http: HttpClient) {}

  getObligationsSummary(): Observable<ObligationsSummaryDto> {
    return this.http.get<ObligationsSummaryDto>(`${this.dashboardUrl}/obligations-summary`);
  }

  getByCycle(cycleId: number): Observable<ExpenseDto[]> {
    return this.http.get<ExpenseDto[]>(`${this.url}/by-cycle/${cycleId}`);
  }

  getById(id: number): Observable<ExpenseDto> {
    return this.http.get<ExpenseDto>(`${this.url}/${id}`);
  }

  getMyObligations(): Observable<MemberObligationDto[]> {
    return this.http.get<MemberObligationDto[]>(`${this.url}/my-obligations`);
  }

  create(payload: {
    expenseCycleId: number;
    title: string;
    amount: number;
    category: string;
    notes?: string;
  }): Observable<ExpenseDto> {
    return this.http.post<ExpenseDto>(this.url, payload);
  }

  update(id: number, payload: {
    title: string;
    amount: number;
    category: string;
    notes?: string;
  }): Observable<ExpenseDto> {
    return this.http.put<ExpenseDto>(`${this.url}/${id}`, payload);
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.url}/${id}`);
  }
}
