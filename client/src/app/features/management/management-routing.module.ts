import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { AppConfigManagementComponent } from './app-config/app-config-management.component';
import { FeatureBugReportsComponent } from './feature-bug-reports/feature-bug-reports.component';
import { UserManagementComponent } from './users/user-management.component';
import { KycReviewComponent } from './kyc-review/kyc-review.component';
import { RoleGuard } from '../../core/guards/role.guard';

const routes: Routes = [
  {
    path: 'app-config',
    component: AppConfigManagementComponent,
    canActivate: [RoleGuard],
    data: { roles: ['Admin', 'SuperAdmin'] }
  },
  {
    path: 'feature-bug-reports',
    component: FeatureBugReportsComponent,
    canActivate: [RoleGuard],
    data: { roles: ['Admin', 'SuperAdmin'] }
  },
  {
    path: 'users',
    component: UserManagementComponent,
    canActivate: [RoleGuard],
    data: { roles: ['Admin', 'SuperAdmin'] }
  },
  {
    path: 'kyc-review',
    component: KycReviewComponent,
    canActivate: [RoleGuard],
    data: { roles: ['Admin', 'SuperAdmin'] }
  }
];

@NgModule({
  imports: [RouterModule.forChild(routes)],
  exports: [RouterModule]
})
export class ManagementRoutingModule { }
