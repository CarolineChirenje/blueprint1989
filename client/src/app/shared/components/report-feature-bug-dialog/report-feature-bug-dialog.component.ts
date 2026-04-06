import { Component, OnInit } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { MatDialogRef } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';
import { FeatureBugReportService } from '../../../core/services/feature-bug-report.service';
import {
  DropdownOption,
  ReportType,
  ReportPriority,
  ReportCategory,
  CreateFeatureBugReportRequest
} from '../../models/feature-bug-report.model';

@Component({
  selector: 'app-report-feature-bug-dialog',
  templateUrl: './report-feature-bug-dialog.component.html',
  styleUrls: ['./report-feature-bug-dialog.component.css'],
  standalone: false
})
export class ReportFeatureBugDialogComponent implements OnInit {
  form!: FormGroup;
  isSubmitting = false;

  typeOptions: DropdownOption[] = [];
  priorityOptions: DropdownOption[] = [];
  categoryOptions: DropdownOption[] = [];

  constructor(
    private fb: FormBuilder,
    private dialogRef: MatDialogRef<ReportFeatureBugDialogComponent>,
    private service: FeatureBugReportService,
    private snackBar: MatSnackBar
  ) {}

  ngOnInit(): void {
    this.typeOptions = this.service.getTypeOptions().filter(o => o.value !== '');
    this.priorityOptions = this.service.getPriorityOptions().filter(o => o.value !== '');
    this.categoryOptions = this.service.getCategoryOptions();

    this.form = this.fb.group({
      title: ['', [Validators.required, Validators.maxLength(200)]],
      description: ['', [Validators.required, Validators.maxLength(2000)]],
      type: [ReportType.Bug, Validators.required],
      priority: [ReportPriority.Medium, Validators.required],
      categories: [[], Validators.required]
    });
  }

  submit(): void {
    if (this.form.invalid || this.isSubmitting) return;

    const val = this.form.value;
    const request: CreateFeatureBugReportRequest = {
      title: val.title,
      description: val.description,
      type: val.type,
      priority: val.priority,
      categories: val.categories
    };

    this.isSubmitting = true;
    this.service.create(request).subscribe({
      next: (report) => {
        this.isSubmitting = false;
        this.snackBar.open('Report submitted successfully', 'Dismiss', { duration: 3000 });
        this.dialogRef.close(report);
      },
      error: (err) => {
        this.isSubmitting = false;
        this.snackBar.open(err?.error?.message || 'Failed to submit report', 'Dismiss', { duration: 4000 });
      }
    });
  }

  cancel(): void {
    this.dialogRef.close();
  }
}
