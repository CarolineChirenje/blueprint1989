import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable, map } from 'rxjs';
import { environment } from '../../../environments/environment';
import { KycStatus } from '../../shared/models/user.model';

export enum KycIdType {
  NationalId = 0,
  Passport = 1,
  DriversLicense = 2,
  NoDocument = 3
}

export interface KycStatusDto {
  status: KycStatus;
  rejectionReason?: string | null;
  submittedAt?: string | null;
  reviewedAt?: string | null;
  canResubmit: boolean;
}

export interface SubmitKycRequest {
  idType: KycIdType;
  idNumber?: string | null;
  fullNameOnId: string;
  documentFileId?: number | null;
  selfieWithIdFileId?: number | null;
}

export interface KycDocumentDto {
  id: number;
  userId: number;
  userName: string;
  userEmail: string;
  idType: KycIdType;
  idNumber?: string | null;
  fullNameOnId: string;
  documentFileUrl?: string | null;
  selfieWithIdFileUrl?: string | null;
  status: KycStatus;
  submittedAt: string;
  rejectionReason?: string | null;
  adminBypassNote?: string | null;
}

@Injectable({ providedIn: 'root' })
export class KycService {
  private apiUrl = `${environment.apiUrl}/kyc`;
  private filesUrl = `${environment.apiUrl}/files`;

  constructor(private http: HttpClient) {}

  getStatus(): Observable<KycStatusDto> {
    return this.http.get<KycStatusDto>(`${this.apiUrl}/status`);
  }

  submit(request: SubmitKycRequest): Observable<KycDocumentDto> {
    return this.http.post<KycDocumentDto>(`${this.apiUrl}/submit`, request);
  }

  getPending(): Observable<KycDocumentDto[]> {
    return this.http.get<KycDocumentDto[]>(`${this.apiUrl}/pending`);
  }

  review(id: number, approve: boolean, rejectionReason?: string): Observable<void> {
    return this.http.post<void>(`${this.apiUrl}/${id}/review`, { approve, rejectionReason });
  }

  bypass(userId: number, note: string): Observable<void> {
    return this.http.post<void>(`${this.apiUrl}/bypass/${userId}`, { note });
  }

  uploadFile(file: File, folder = 'kyc'): Observable<number> {
    const formData = new FormData();
    formData.append('file', file);

    return this.http.post<{ url: string }>(`${this.filesUrl}/upload?folder=${encodeURIComponent(folder)}`, formData).pipe(
      map(resp => {
        const match = /\/api\/files\/(\d+)$/.exec(resp.url);
        if (!match) throw new Error('Could not parse uploaded file ID.');
        return Number(match[1]);
      })
    );
  }
}
