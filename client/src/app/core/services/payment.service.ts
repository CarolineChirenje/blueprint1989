import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { PaymentDto } from '../../shared/models/expense-cycle.model';

@Injectable({ providedIn: 'root' })
export class PaymentService {
  private url = `${environment.apiUrl}/payments`;

  constructor(private http: HttpClient) {}

  getByCycle(cycleId: number): Observable<PaymentDto[]> {
    return this.http.get<PaymentDto[]>(`${this.url}/by-cycle/${cycleId}`);
  }

  getMine(): Observable<PaymentDto[]> {
    return this.http.get<PaymentDto[]>(`${this.url}/mine`);
  }

  create(payload: {
    payeeId: number;
    expenseCycleId: number;
    amount: number;
    notes?: string;
  }): Observable<PaymentDto> {
    return this.http.post<PaymentDto>(this.url, payload);
  }

  respond(paymentId: number, confirm: boolean, notes?: string): Observable<PaymentDto> {
    return this.http.post<PaymentDto>(`${this.url}/${paymentId}/respond`, { confirm, notes });
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.url}/${id}`);
  }
}
