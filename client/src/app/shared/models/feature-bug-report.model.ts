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
  Low = 1,
  Medium = 2,
  High = 3
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
  versionNumber: string | null;
  submittedByName: string;
  submittedAt: string;
  updatedAt: string;
}

export interface UpdateReportStatusRequest {
  status: number;
  versionNumber?: string;
}

export interface DropdownOption {
  value: number;
  label: string;
}
