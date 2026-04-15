import { Component, Inject, OnInit } from '@angular/core';
import { MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';

@Component({
  selector: 'app-close-report-dialog',
  templateUrl: './close-report-dialog.component.html',
  styleUrls: ['./close-report-dialog.component.css'],
  standalone: false
})
export class CloseReportDialogComponent implements OnInit {
  versionForm!: FormGroup;

  constructor(
    public dialogRef: MatDialogRef<CloseReportDialogComponent>,
    @Inject(MAT_DIALOG_DATA) public data: any,
    private fb: FormBuilder
  ) {
    this.versionForm = this.fb.group({
      versionNumber: ['', [Validators.required, Validators.pattern(/^\d+\.\d+\.\d+$/)]]
    });
  }

  ngOnInit(): void {}

  submit(): void {
    if (this.versionForm.valid) {
      this.dialogRef.close({
        versionNumber: this.versionForm.get('versionNumber')?.value
      });
    }
  }

  cancel(): void {
    this.dialogRef.close();
  }

  getVersionError(): string {
    const control = this.versionForm.get('versionNumber');
    if (!control || !control.errors) {
      return '';
    }

    if (control.hasError('required')) {
      return 'Version number is required';
    }
    if (control.hasError('pattern')) {
      return 'Version must be in format: X.Y.Z (e.g., 1.0.0)';
    }
    return '';
  }

  isVersionInvalid(): boolean {
    const control = this.versionForm.get('versionNumber');
    return !!(control && control.invalid && (control.dirty || control.touched));
  }
}
