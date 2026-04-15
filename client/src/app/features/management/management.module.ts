import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormsModule } from '@angular/forms';
import { ManagementRoutingModule } from './management-routing.module';
import { AppConfigManagementComponent } from './app-config/app-config-management.component';
import { FeatureBugReportsComponent } from './feature-bug-reports/feature-bug-reports.component';
import { UserManagementComponent } from './users/user-management.component';
import { KycReviewComponent } from './kyc-review/kyc-review.component';
import { UserGroupRolesDialogComponent } from './users/user-group-roles-dialog/user-group-roles-dialog.component';
import { EditUserDialogComponent } from './users/edit-user-dialog/edit-user-dialog.component';
import { AdminResetPasswordDialogComponent } from './users/admin-reset-password-dialog/admin-reset-password-dialog.component';
import { SharedModule } from '../../shared/shared.module';

@NgModule({
  declarations: [
    AppConfigManagementComponent,
    FeatureBugReportsComponent,
    UserManagementComponent,
    KycReviewComponent,
    UserGroupRolesDialogComponent,
    EditUserDialogComponent,
    AdminResetPasswordDialogComponent
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
