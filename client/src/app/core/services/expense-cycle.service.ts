import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  ExpenseCycleDto,
  ExpenseCycleSummaryDto,
  CycleBalanceDto,
  CycleContributionSummaryDto,
  OutstandingSummaryDto,
  CurrencyDto,
  MukandoRoundDto,
  MukandoPayoutDto,
  MukandoSwapRequestDto,
  OptOutRequestDto,
  MukandoRoundActivityDto,
  MukandoCycleSummaryDto,
  MukandoDashboardDto,
  MukandoVerificationRequestDto,
  CycleAgreementSummaryDto
} from '../../shared/models/expense-cycle.model';

@Injectable({ providedIn: 'root' })
export class ExpenseCycleService {
  private url = `${environment.apiUrl}/cycles`;

  constructor(private http: HttpClient) {}

  // ── Existing endpoints ──────────────────────────────────────────────────────

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

  create(payload: {
    name: string;
    startDate: string;
    endDate: string;
    memberUserIds: number[] | null;
    groupId?: number | null;
    currencyId: number;
    cycleType?: string;
    contributionAmount?: number | null;
    frequency?: string | null;
    payoutOrder?: number[] | null;
    copyExpensesFromCycleId?: number | null;
  }): Observable<ExpenseCycleDto> {
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

  addMember(cycleId: number, userId: number, cycleRole: 'Participant' | 'Observer' = 'Participant'): Observable<void> {
    return this.http.post<void>(`${this.url}/${cycleId}/members/${userId}?cycleRole=${cycleRole}`, {});
  }

  addMembersBatch(cycleId: number, userIds: number[], cycleRole: 'Participant' | 'Observer' = 'Participant'): Observable<{ addedUserIds: number[] }> {
    return this.http.post<{ addedUserIds: number[] }>(`${this.url}/${cycleId}/members/batch`, { userIds, cycleRole });
  }

  removeMember(cycleId: number, userId: number): Observable<void> {
    return this.http.delete<void>(`${this.url}/${cycleId}/members/${userId}`);
  }

  getOutstandingSummary(): Observable<OutstandingSummaryDto> {
    return this.http.get<OutstandingSummaryDto>(`${this.url}/outstanding-summary`);
  }

  // ── Currencies ──────────────────────────────────────────────────────────────

  getCurrencies(): Observable<CurrencyDto[]> {
    return this.http.get<CurrencyDto[]>(`${environment.apiUrl}/currencies`);
  }

  // ── Mukando Rounds ──────────────────────────────────────────────────────────

  getRounds(cycleId: number): Observable<MukandoRoundDto[]> {
    return this.http.get<MukandoRoundDto[]>(`${this.url}/${cycleId}/rounds`);
  }

  getRoundDetail(cycleId: number, roundId: number): Observable<MukandoRoundDto> {
    return this.http.get<MukandoRoundDto>(`${this.url}/${cycleId}/rounds/${roundId}`);
  }

  getPayout(cycleId: number, roundId: number): Observable<MukandoPayoutDto> {
    return this.http.get<MukandoPayoutDto>(`${this.url}/${cycleId}/rounds/${roundId}/payout`);
  }

  recordContribution(cycleId: number, roundId: number, payload: { reference?: string; notes?: string }, proof?: File): Observable<void> {
    const formData = new FormData();
    if (payload.reference) formData.append('reference', payload.reference);
    if (payload.notes) formData.append('notes', payload.notes);
    if (proof) formData.append('proof', proof);
    return this.http.post<void>(`${this.url}/${cycleId}/rounds/${roundId}/contribute`, formData);
  }

  confirmContribution(cycleId: number, roundId: number, memberId: number): Observable<void> {
    return this.http.post<void>(`${this.url}/${cycleId}/rounds/${roundId}/confirm-contribution/${memberId}`, {});
  }

  recordPayout(cycleId: number, roundId: number, payload: { amountDisbursed: number; paymentMethod: string; reference?: string }, proof?: File): Observable<void> {
    const formData = new FormData();
    formData.append('amountDisbursed', payload.amountDisbursed.toString());
    formData.append('paymentMethod', payload.paymentMethod);
    if (payload.reference) formData.append('reference', payload.reference);
    if (proof) formData.append('proof', proof);
    return this.http.post<void>(`${this.url}/${cycleId}/rounds/${roundId}/payout`, formData);
  }

  forceCloseRound(cycleId: number, roundId: number): Observable<void> {
    return this.http.post<void>(`${this.url}/${cycleId}/rounds/${roundId}/force-close`, {});
  }

  // ── Mukando Settings ────────────────────────────────────────────────────────

  updateMukandoSettings(cycleId: number, payload: { contributionAmount: number; frequency: string; payoutOrder: number[] }): Observable<void> {
    return this.http.put<void>(`${this.url}/${cycleId}/mukando-settings`, payload);
  }

  // ── Summary & Export ────────────────────────────────────────────────────────

  getMukandoSummary(cycleId: number): Observable<MukandoCycleSummaryDto> {
    return this.http.get<MukandoCycleSummaryDto>(`${this.url}/${cycleId}/mukando-summary`);
  }

  getRoundActivity(cycleId: number, roundId: number): Observable<MukandoRoundActivityDto[]> {
    return this.http.get<MukandoRoundActivityDto[]>(`${this.url}/${cycleId}/rounds/${roundId}/activity`);
  }

  exportCycleCsv(cycleId: number): Observable<Blob> {
    return this.http.get(`${this.url}/${cycleId}/export`, { responseType: 'blob' });
  }

  duplicateCycle(cycleId: number, newStartDate: string): Observable<ExpenseCycleDto> {
    return this.http.post<ExpenseCycleDto>(`${this.url}/${cycleId}/duplicate`, { newStartDate });
  }

  // ── Dashboard ───────────────────────────────────────────────────────────────

  getMukandoDashboard(): Observable<MukandoDashboardDto> {
    return this.http.get<MukandoDashboardDto>(`${this.url}/mukando-dashboard`);
  }

  // ── Swap Requests ───────────────────────────────────────────────────────────

  getSwapRequests(cycleId: number): Observable<MukandoSwapRequestDto[]> {
    return this.http.get<MukandoSwapRequestDto[]>(`${this.url}/${cycleId}/swap-requests`);
  }

  createSwapRequest(cycleId: number, targetUserId: number): Observable<MukandoSwapRequestDto> {
    return this.http.post<MukandoSwapRequestDto>(`${this.url}/${cycleId}/swap-requests`, { targetUserId });
  }

  respondSwapRequest(cycleId: number, swapId: number, accept: boolean): Observable<void> {
    return this.http.post<void>(`${this.url}/${cycleId}/swap-requests/${swapId}/respond`, { accept });
  }

  cancelSwapRequest(cycleId: number, swapId: number): Observable<void> {
    return this.http.delete<void>(`${this.url}/${cycleId}/swap-requests/${swapId}`);
  }

  // ── Opt-out Requests ────────────────────────────────────────────────────────

  getOptOutRequests(cycleId: number): Observable<OptOutRequestDto[]> {
    return this.http.get<OptOutRequestDto[]>(`${this.url}/${cycleId}/opt-out-requests`);
  }

  createOptOutRequest(cycleId: number, reason: string): Observable<OptOutRequestDto> {
    return this.http.post<OptOutRequestDto>(`${this.url}/${cycleId}/opt-out`, { reason });
  }

  respondOptOutRequest(cycleId: number, requestId: number, approve: boolean): Observable<void> {
    return this.http.post<void>(`${this.url}/${cycleId}/opt-out/${requestId}/respond`, { approve });
  }

  // ── File Upload ─────────────────────────────────────────────────────────────

  uploadFile(file: File, folder: string = 'proofs'): Observable<{ url: string }> {
    const formData = new FormData();
    formData.append('file', file);
    return this.http.post<{ url: string }>(`${environment.apiUrl}/files/upload?folder=${encodeURIComponent(folder)}`, formData);
  }

  // ── Verification ────────────────────────────────────────────────────────────

  getPendingVerifications(cycleId: number): Observable<MukandoVerificationRequestDto[]> {
    return this.http.get<MukandoVerificationRequestDto[]>(`${this.url}/${cycleId}/pending-verifications`);
  }

  verifyContribution(cycleId: number, roundId: number, verificationId: number, approve: boolean, rejectionReason?: string): Observable<void> {
    return this.http.post<void>(`${this.url}/${cycleId}/rounds/${roundId}/verify-contribution/${verificationId}`, { approve, rejectionReason });
  }

  verifyPayout(cycleId: number, roundId: number, verificationId: number, approve: boolean, rejectionReason?: string): Observable<void> {
    return this.http.post<void>(`${this.url}/${cycleId}/rounds/${roundId}/verify-payout/${verificationId}`, { approve, rejectionReason });
  }

  reassignVerifier(cycleId: number, roundId: number, verificationId: number): Observable<void> {
    return this.http.post<void>(`${this.url}/${cycleId}/rounds/${roundId}/reassign-verifier/${verificationId}`, {});
  }

  getAgreementStatus(cycleId: number): Observable<CycleAgreementSummaryDto> {
    return this.http.get<CycleAgreementSummaryDto>(`${this.url}/${cycleId}/agreements`);
  }

  submitAgreement(cycleId: number): Observable<CycleAgreementSummaryDto> {
    return this.http.post<CycleAgreementSummaryDto>(`${this.url}/${cycleId}/agree`, {});
  }
}
