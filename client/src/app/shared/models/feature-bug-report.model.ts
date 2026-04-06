export enum ReportStatus {
  New = 'New',
  InReview = 'InReview',
  Closed = 'Closed'
}

export enum ReportType {
  Feature = 'Feature',
  Bug = 'Bug'
}

export enum ReportPriority {
  Critical = 'Critical',
  High = 'High',
  Medium = 'Medium',
  Low = 'Low'
}

export enum ReportCategory {
  UI = 'UI',
  Backend = 'Backend',
  Performance = 'Performance',
  Security = 'Security',
  API = 'API',
  Mobile = 'Mobile',
  Desktop = 'Desktop',
  Other = 'Other'
}

export interface FeatureBugReportResponseDto {
  id: number;
  title: string;
  description: string;
  type: ReportType;
  typeName: string;
  status: ReportStatus;
  statusName: string;
  priority: ReportPriority;
  priorityName: string;
  categories: ReportCategory[];
  categoryNames: string[];
  versionNumber: string | null;
  submittedByUserId: number;
  submittedByName: string;
  submittedAt: string;
  updatedAt: string;
  imageUrl: string | null;
}

export interface CreateFeatureBugReportRequest {
  title: string;
  description: string;
  type: ReportType;
  priority: ReportPriority;
  categories: ReportCategory[];
}

export interface UpdateFeatureBugReportRequest {
  title: string;
  description: string;
  type: ReportType;
  priority: ReportPriority;
  categories: ReportCategory[];
}

export interface UpdateReportStatusRequest {
  status: ReportStatus;
  versionNumber?: string;
}

export interface DropdownOption {
  value: string;
  label: string;
}
