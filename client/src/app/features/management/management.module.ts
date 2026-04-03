import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormsModule } from '@angular/forms';
import { ManagementRoutingModule } from './management-routing.module';
import { AppConfigManagementComponent } from './app-config/app-config-management.component';
import { FeatureBugReportsComponent } from './feature-bug-reports/feature-bug-reports.component';
import { GroupManagementComponent } from './groups/group-management.component';
import { CreateGroupDialogComponent } from './groups/create-group-dialog/create-group-dialog.component';
import { GroupMembersDialogComponent } from './groups/group-members-dialog/group-members-dialog.component';
import { UserManagementComponent } from './users/user-management.component';
import { UserGroupRolesDialogComponent } from './users/user-group-roles-dialog/user-group-roles-dialog.component';
import { SharedModule } from '../../shared/shared.module';

@NgModule({
  declarations: [
    AppConfigManagementComponent,
    FeatureBugReportsComponent,
    GroupManagementComponent,
    CreateGroupDialogComponent,
    GroupMembersDialogComponent,
    UserManagementComponent,
    UserGroupRolesDialogComponent
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
