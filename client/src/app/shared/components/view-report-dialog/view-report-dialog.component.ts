import { Component, Inject } from '@angular/core';
import { MatDialogRef, MAT_DIALOG_DATA, MatDialog } from '@angular/material/dialog';
import { FeatureBugReportResponseDto, ReportStatus, UpdateReportStatusRequest } from '../../models/feature-bug-report.model';
import { FeatureBugReportService } from '../../../core/services/feature-bug-report.service';

export interface ViewReportDialogData {
  report: FeatureBugReportResponseDto;
  isAdmin: boolean;
}

@Component({
  selector: 'app-view-report-dialog',
  templateUrl: './view-report-dialog.component.html',
  styleUrls: ['./view-report-dialog.component.css'],
  standalone: false
})
export class ViewReportDialogComponent {
  report: FeatureBugReportResponseDto;
  isAdmin: boolean;
  isUpdating = false;
  errorMessage = '';

  readonly ReportStatus = ReportStatus;

  constructor(
    public dialogRef: MatDialogRef<ViewReportDialogComponent>,
    @Inject(MAT_DIALOG_DATA) public data: ViewReportDialogData,
    private featureBugReportService: FeatureBugReportService
  ) {
    this.report = data.report;
    this.isAdmin = data.isAdmin;
  }

  close(): void {
    this.dialogRef.close();
  }

  markInReview(): void {
    if (this.isUpdating) return;
    this.isUpdating = true;
    this.errorMessage = '';
    this.featureBugReportService.updateStatus(this.report.id, { status: ReportStatus.InReview }).subscribe({
      next: (updated) => {
        this.report = updated;
        this.isUpdating = false;
        this.dialogRef.close(updated);
      },
      error: () => {
        this.isUpdating = false;
        this.errorMessage = 'Failed to update status.';
      }
    });
  }

  getStatusClass(): string {
    switch (this.report.status) {
      case ReportStatus.New: return 'badge-new';
      case ReportStatus.InReview: return 'badge-review';
      case ReportStatus.Closed: return 'badge-closed';
      default: return '';
    }
  }
}
