import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ExpenseDisputeDto } from '../../shared/models/expense-cycle.model';

@Injectable({ providedIn: 'root' })
export class ExpenseDisputeService {
  private url = `${environment.apiUrl}/disputes`;

  constructor(private http: HttpClient) {}

  getByCycle(cycleId: number): Observable<ExpenseDisputeDto[]> {
    return this.http.get<ExpenseDisputeDto[]>(this.url, { params: { cycleId } });
  }

  create(payload: { expenseId: number; reason: string }): Observable<ExpenseDisputeDto> {
    return this.http.post<ExpenseDisputeDto>(this.url, payload);
  }

  updateStatus(disputeId: number, payload: { status: string; adminNotes?: string | null }): Observable<ExpenseDisputeDto> {
    return this.http.patch<ExpenseDisputeDto>(`${this.url}/${disputeId}/status`, payload);
  }
}
