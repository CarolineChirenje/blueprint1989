import { NgModule } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { RouterModule } from '@angular/router';
import { ReactiveFormsModule } from '@angular/forms';
import { MaterialModule } from './material.module';
import { ConfirmDialogComponent } from './components/confirm-dialog/confirm-dialog.component';
import { PasswordFieldComponent } from './components/password-field/password-field.component';
import { CloseReportDialogComponent } from './components/close-report-dialog/close-report-dialog.component';
import { ViewReportDialogComponent } from './components/view-report-dialog/view-report-dialog.component';
import { ReportFeatureBugDialogComponent } from './components/report-feature-bug-dialog/report-feature-bug-dialog.component';
import { DivvyChartComponent } from './components/vitara-chart/vitara-chart.component';

@NgModule({
  declarations: [
    ConfirmDialogComponent,
    PasswordFieldComponent,
    CloseReportDialogComponent,
    ViewReportDialogComponent,
    ReportFeatureBugDialogComponent,
    DivvyChartComponent
  ],
  imports: [
    CommonModule,
    RouterModule,
    ReactiveFormsModule,
    MaterialModule,
    DatePipe
  ],
  exports: [
    ConfirmDialogComponent,
    PasswordFieldComponent,
    CloseReportDialogComponent,
    ViewReportDialogComponent,
    ReportFeatureBugDialogComponent,
    DivvyChartComponent,
    RouterModule,
    ReactiveFormsModule,
    MaterialModule,
    DatePipe
  ]
})
export class SharedModule { }
