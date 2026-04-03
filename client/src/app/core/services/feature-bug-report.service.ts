import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  FeatureBugReportResponseDto,
  UpdateReportStatusRequest,
  DropdownOption,
  ReportStatus,
  ReportType,
  ReportPriority
} from '../../shared/models/feature-bug-report.model';

@Injectable({ providedIn: 'root' })
export class FeatureBugReportService {
  private readonly apiUrl = `${environment.apiUrl}/feature-bug-reports`;

  constructor(private http: HttpClient) {}

  getFiltered(
    status?: number,
    type?: number,
    priority?: number
  ): Observable<FeatureBugReportResponseDto[]> {
    let params = new HttpParams();
    if (status !== undefined) params = params.set('status', status);
    if (type !== undefined) params = params.set('type', type);
    if (priority !== undefined) params = params.set('priority', priority);
    return this.http.get<FeatureBugReportResponseDto[]>(this.apiUrl, { params });
  }

  updateStatus(id: number, body: UpdateReportStatusRequest): Observable<FeatureBugReportResponseDto> {
    return this.http.put<FeatureBugReportResponseDto>(`${this.apiUrl}/${id}/status`, body);
  }

  deleteReport(id: number): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${id}`);
  }

  getStatusOptions(): DropdownOption[] {
    return [
      { value: 0, label: 'All Statuses' },
      { value: ReportStatus.New, label: 'New' },
      { value: ReportStatus.InReview, label: 'In Review' },
      { value: ReportStatus.Closed, label: 'Closed' }
    ];
  }

  getTypeOptions(): DropdownOption[] {
    return [
      { value: 0, label: 'All Types' },
      { value: ReportType.Feature, label: 'Feature Request' },
      { value: ReportType.Bug, label: 'Bug Report' }
    ];
  }

  getPriorityOptions(): DropdownOption[] {
    return [
      { value: 0, label: 'All Priorities' },
      { value: ReportPriority.Low, label: 'Low' },
      { value: ReportPriority.Medium, label: 'Medium' },
      { value: ReportPriority.High, label: 'High' }
    ];
  }
}
