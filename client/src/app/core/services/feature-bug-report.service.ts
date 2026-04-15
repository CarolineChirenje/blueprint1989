import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  FeatureBugReportResponseDto,
  UpdateReportStatusRequest,
  CreateFeatureBugReportRequest,
  UpdateFeatureBugReportRequest,
  DropdownOption,
  ReportStatus,
  ReportType,
  ReportPriority,
  ReportCategory
} from '../../shared/models/feature-bug-report.model';

@Injectable({ providedIn: 'root' })
export class FeatureBugReportService {
  private readonly apiUrl = `${environment.apiUrl}/feature-bug-reports`;

  constructor(private http: HttpClient) {}

  getFiltered(
    status?: string,
    type?: string,
    priority?: string
  ): Observable<FeatureBugReportResponseDto[]> {
    let params = new HttpParams();
    if (status) params = params.set('status', status);
    if (type) params = params.set('type', type);
    if (priority) params = params.set('priority', priority);
    return this.http.get<FeatureBugReportResponseDto[]>(this.apiUrl, { params });
  }

  updateStatus(id: number, body: UpdateReportStatusRequest): Observable<FeatureBugReportResponseDto> {
    return this.http.put<FeatureBugReportResponseDto>(`${this.apiUrl}/${id}/status`, body);
  }

  deleteReport(id: number): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${id}`);
  }

  create(body: CreateFeatureBugReportRequest, image?: File): Observable<FeatureBugReportResponseDto> {
    const formData = new FormData();
    formData.append('title', body.title);
    formData.append('description', body.description);
    formData.append('type', body.type);
    formData.append('priority', body.priority);
    body.categories.forEach(cat => formData.append('categories', cat));
    if (image) {
      formData.append('image', image);
    }
    return this.http.post<FeatureBugReportResponseDto>(this.apiUrl, formData);
  }

  update(id: number, body: UpdateFeatureBugReportRequest): Observable<FeatureBugReportResponseDto> {
    return this.http.put<FeatureBugReportResponseDto>(`${this.apiUrl}/${id}`, body);
  }

  getMyReports(): Observable<FeatureBugReportResponseDto[]> {
    return this.http.get<FeatureBugReportResponseDto[]>(`${this.apiUrl}/my-reports`);
  }

  getMyReportById(id: number): Observable<FeatureBugReportResponseDto> {
    return this.http.get<FeatureBugReportResponseDto>(`${this.apiUrl}/my-reports/${id}`);
  }

  getStatusOptions(): DropdownOption[] {
    return [
      { value: '', label: 'All Statuses' },
      { value: ReportStatus.New, label: 'New' },
      { value: ReportStatus.InReview, label: 'In Review' },
      { value: ReportStatus.Closed, label: 'Closed' }
    ];
  }

  getTypeOptions(): DropdownOption[] {
    return [
      { value: '', label: 'All Types' },
      { value: ReportType.Feature, label: 'Feature Request' },
      { value: ReportType.Bug, label: 'Bug Report' }
    ];
  }

  getPriorityOptions(): DropdownOption[] {
    return [
      { value: '', label: 'All Priorities' },
      { value: ReportPriority.Critical, label: 'Critical' },
      { value: ReportPriority.High, label: 'High' },
      { value: ReportPriority.Medium, label: 'Medium' },
      { value: ReportPriority.Low, label: 'Low' }
    ];
  }

  getCategoryOptions(): DropdownOption[] {
    return [
      { value: ReportCategory.UI, label: 'User Interface' },
      { value: ReportCategory.Backend, label: 'Backend/Server' },
      { value: ReportCategory.Performance, label: 'Performance' },
      { value: ReportCategory.Security, label: 'Security' },
      { value: ReportCategory.API, label: 'API' },
      { value: ReportCategory.Mobile, label: 'Mobile' },
      { value: ReportCategory.Desktop, label: 'Desktop' },
      { value: ReportCategory.Other, label: 'Other' }
    ];
  }
}
