import { Component, OnInit } from '@angular/core';
import { MatDialog } from '@angular/material/dialog';
import { FeatureBugReportService } from '../../../core/services/feature-bug-report.service';
import { FeatureBugReportResponseDto, DropdownOption, ReportStatus } from '../../../shared/models/feature-bug-report.model';
import { ViewReportDialogComponent } from '../../../shared/components/view-report-dialog/view-report-dialog.component';
import { CloseReportDialogComponent } from '../../../shared/components/close-report-dialog/close-report-dialog.component';
import { ConfirmDialogComponent } from '../../../shared/components/confirm-dialog/confirm-dialog.component';
import { MatSnackBar } from '@angular/material/snack-bar';

@Component({
  selector: 'app-feature-bug-reports',
  templateUrl: './feature-bug-reports.component.html',
  styleUrls: ['./feature-bug-reports.component.css'],
  standalone: false
})
export class FeatureBugReportsComponent implements OnInit {
  filteredReports: FeatureBugReportResponseDto[] = [];
  processingIds = new Set<number>();

  statusFilter = 0;
  typeFilter = 0;
  priorityFilter = 0;

  isLoading = false;
  successMessage = '';
  errorMessage = '';

  statusOptions: DropdownOption[] = [];
  typeOptions: DropdownOption[] = [];
  priorityOptions: DropdownOption[] = [];

  constructor(
    private featureBugReportService: FeatureBugReportService,
    private dialog: MatDialog,
    private snackBar: MatSnackBar
  ) {}

  ngOnInit(): void {
    this.loadOptions();
    this.loadReports();
  }

  loadOptions(): void {
    this.statusOptions = this.featureBugReportService.getStatusOptions();
    this.typeOptions = this.featureBugReportService.getTypeOptions();
    this.priorityOptions = this.featureBugReportService.getPriorityOptions();
  }

  loadReports(): void {
    this.isLoading = true;
    this.errorMessage = '';
    this.successMessage = '';

    this.featureBugReportService.getFiltered(
      this.statusFilter || undefined,
      this.typeFilter || undefined,
      this.priorityFilter || undefined
    ).subscribe({
      next: (data) => {
        this.filteredReports = data;
        this.isLoading = false;
      },
      error: (error) => {
        this.isLoading = false;
        const errorMsg = error?.error?.message || 'Failed to load reports';
        this.errorMessage = errorMsg;
        this.snackBar.open(errorMsg, 'Dismiss', { duration: 4000 });
      }
    });
  }

  onStatusFilterChange(): void { this.loadReports(); }
  onTypeFilterChange(): void { this.loadReports(); }
  onPriorityFilterChange(): void { this.loadReports(); }

  openViewDialog(report: FeatureBugReportResponseDto): void {
    this.dialog.open(ViewReportDialogComponent, {
      width: '600px',
      data: { report, isAdmin: true },
      panelClass: 'report-dialog'
    }).afterClosed().subscribe((updated: FeatureBugReportResponseDto | undefined) => {
      if (updated) { this.loadReports(); }
    });
  }

  markInReview(id: number): void {
    if (this.processingIds.has(id)) return;
    this.processingIds.add(id);
    this.featureBugReportService.updateStatus(id, { status: ReportStatus.InReview }).subscribe({
      next: () => {
        this.processingIds.delete(id);
        this.loadReports();
        this.snackBar.open('Report marked as in review', 'Dismiss', { duration: 3000 });
      },
      error: (error) => {
        this.processingIds.delete(id);
        this.snackBar.open(error?.error?.message || 'Failed to update report', 'Dismiss', { duration: 4000 });
      }
    });
  }

  openCloseDialog(id: number): void {
    this.dialog.open(CloseReportDialogComponent, {
      width: '500px',
      data: { reportId: id },
      panelClass: 'report-dialog'
    }).afterClosed().subscribe((result) => {
      if (result) { this.closeReport(id, result.versionNumber); }
    });
  }

  closeReport(id: number, versionNumber: string): void {
    if (this.processingIds.has(id)) return;
    this.processingIds.add(id);
    this.featureBugReportService.updateStatus(id, { status: ReportStatus.Closed, versionNumber }).subscribe({
      next: () => {
        this.processingIds.delete(id);
        this.loadReports();
        this.snackBar.open(`Report closed with version ${versionNumber}`, 'Dismiss', { duration: 3000 });
      },
      error: (error) => {
        this.processingIds.delete(id);
        this.snackBar.open(error?.error?.message || 'Failed to close report', 'Dismiss', { duration: 4000 });
      }
    });
  }

  canMarkInReview(status: number, id: number): boolean {
    return status === ReportStatus.New && !this.processingIds.has(id);
  }

  canClose(status: number, id: number): boolean {
    return status < ReportStatus.Closed && !this.processingIds.has(id);
  }

  deleteReport(id: number): void {
    if (this.processingIds.has(id)) return;
    this.dialog.open(ConfirmDialogComponent, {
      width: '400px',
      data: {
        title: 'Delete Report',
        message: 'Are you sure you want to delete this report? This action cannot be undone.',
        confirmText: 'Delete',
        cancelText: 'Cancel',
        confirmColor: 'warn'
      }
    }).afterClosed().subscribe((confirmed: boolean) => {
      if (!confirmed) return;
      this.processingIds.add(id);
      this.featureBugReportService.deleteReport(id).subscribe({
        next: () => {
          this.processingIds.delete(id);
          this.loadReports();
          this.snackBar.open('Report deleted', 'Dismiss', { duration: 3000 });
        },
        error: (error) => {
          this.processingIds.delete(id);
          this.snackBar.open(error?.error?.message || 'Failed to delete report', 'Dismiss', { duration: 4000 });
        }
      });
    });
  }

  getStatusBadgeClass(status: number): string {
    switch (status) {
      case ReportStatus.New: return 'badge-new';
      case ReportStatus.InReview: return 'badge-review';
      case ReportStatus.Closed: return 'badge-closed';
      default: return '';
    }
  }

  trackByReportId(_index: number, report: FeatureBugReportResponseDto): number {
    return report.id;
  }
}
