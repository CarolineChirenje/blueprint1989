import { ChangeDetectorRef, Component, OnInit } from '@angular/core';
import { MatDialog } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';
import { FeatureBugReportService } from '../../../../core/services/feature-bug-report.service';
import { FeatureBugReportResponseDto, ReportStatus } from '../../../../shared/models/feature-bug-report.model';
import { ViewReportDialogComponent } from '../../../../shared/components/view-report-dialog/view-report-dialog.component';
import { ReportFeatureBugDialogComponent } from '../../../../shared/components/report-feature-bug-dialog/report-feature-bug-dialog.component';
import { ConfirmDialogComponent } from '../../../../shared/components/confirm-dialog/confirm-dialog.component';

@Component({
  selector: 'app-my-reports',
  templateUrl: './my-reports.component.html',
  styleUrls: ['./my-reports.component.css', '../../../../shared/styles/table.css'],
  standalone: false
})
export class MyReportsComponent implements OnInit {
  reports: FeatureBugReportResponseDto[] = [];
  isLoading = false;
  processingIds = new Set<number>();

  readonly ReportStatus = ReportStatus;

  constructor(
    private service: FeatureBugReportService,
    private dialog: MatDialog,
    private snackBar: MatSnackBar,
    private cdr: ChangeDetectorRef
  ) {}

  ngOnInit(): void {
    this.loadReports();
  }

  loadReports(): void {
    this.isLoading = true;
    this.service.getMyReports().subscribe({
      next: (data) => {
        this.reports = data;
        this.isLoading = false;
        this.cdr.detectChanges();
      },
      error: () => {
        this.isLoading = false;
        this.snackBar.open('Failed to load your reports', 'Dismiss', { duration: 4000 });
        this.cdr.detectChanges();
      }
    });
  }

  openNewReport(): void {
    this.dialog.open(ReportFeatureBugDialogComponent, {
      width: '600px',
      panelClass: 'report-dialog'
    }).afterClosed().subscribe((result) => {
      if (result) this.loadReports();
    });
  }

  openViewDialog(report: FeatureBugReportResponseDto): void {
    this.dialog.open(ViewReportDialogComponent, {
      width: '600px',
      data: { report, isAdmin: false },
      panelClass: 'report-dialog'
    });
  }

  deleteReport(id: number): void {
    if (this.processingIds.has(id)) return;
    this.dialog.open(ConfirmDialogComponent, {
      width: '400px',
      data: {
        title: 'Delete Report',
        message: 'Are you sure you want to delete this report?',
        confirmText: 'Delete',
        cancelText: 'Cancel',
        confirmColor: 'warn'
      }
    }).afterClosed().subscribe((confirmed: boolean) => {
      if (!confirmed) return;
      this.processingIds.add(id);
      this.service.deleteReport(id).subscribe({
        next: () => {
          this.processingIds.delete(id);
          this.loadReports();
          this.snackBar.open('Report deleted', 'Dismiss', { duration: 3000 });
        },
        error: (err) => {
          this.processingIds.delete(id);
          this.snackBar.open(err?.error?.message || 'Failed to delete report', 'Dismiss', { duration: 4000 });
          this.cdr.detectChanges();
        }
      });
    });
  }

  canDelete(status: number): boolean {
    return status === ReportStatus.New;
  }

  getStatusBadgeClass(status: number): string {
    switch (status) {
      case ReportStatus.New: return 'badge-new';
      case ReportStatus.InReview: return 'badge-review';
      case ReportStatus.Closed: return 'badge-closed';
      default: return '';
    }
  }
}
