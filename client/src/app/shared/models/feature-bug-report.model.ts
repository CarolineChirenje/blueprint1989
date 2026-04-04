export enum ReportStatus {
  New = 1,
  InReview = 2,
  Closed = 3
}

export enum ReportType {
  Feature = 1,
  Bug = 2
}

export enum ReportPriority {
  Critical = 1,
  High = 2,
  Medium = 3,
  Low = 4
}

export enum ReportCategory {
  UI = 1,
  Backend = 2,
  Performance = 3,
  Security = 4,
  API = 5,
  Mobile = 6,
  Desktop = 7,
  Other = 8
}

export interface FeatureBugReportResponseDto {
  id: number;
  title: string;
  description: string;
  type: number;
  typeName: string;
  status: number;
  statusName: string;
  priority: number;
  priorityName: string;
  categories: number[];
  categoryNames: string[];
  versionNumber: string | null;
  submittedByUserId: number;
  submittedByName: string;
  submittedAt: string;
  updatedAt: string;
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
  status: number;
  versionNumber?: string;
}

export interface DropdownOption {
  value: number;
  label: string;
}
