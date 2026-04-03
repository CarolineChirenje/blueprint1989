import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormsModule } from '@angular/forms';
import { ManagementRoutingModule } from './management-routing.module';
import { AppConfigManagementComponent } from './app-config/app-config-management.component';
import { FeatureBugReportsComponent } from './feature-bug-reports/feature-bug-reports.component';
import { SharedModule } from '../../shared/shared.module';

@NgModule({
  declarations: [
    AppConfigManagementComponent,
    FeatureBugReportsComponent
  ],
  imports: [
    CommonModule,
    ReactiveFormsModule,
    FormsModule,
    ManagementRoutingModule,
    SharedModule
  ]
})
export class ManagementModule { }
