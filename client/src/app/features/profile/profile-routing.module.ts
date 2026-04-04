import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { ProfileComponent } from './components/profile.component';
import { MfaSetupComponent } from './components/mfa-setup.component';
import { LinkedDevicesComponent } from './components/linked-devices.component';
import { BiometricSetupComponent } from './components/biometric-setup.component';
import { NotificationPreferencesComponent } from './components/notification-preferences/notification-preferences.component';
import { MyReportsComponent } from './components/my-reports/my-reports.component';
import { RoleGuard } from '../../core/guards/role.guard';

const routes: Routes = [
  {
    path: '',
    component: ProfileComponent,
    canActivate: [RoleGuard],
    data: { roles: ['SuperAdmin', 'Admin', 'Member'] }
  },
  {
    path: 'security',
    component: MfaSetupComponent,
    canActivate: [RoleGuard],
    data: { roles: ['SuperAdmin', 'Admin', 'Member'] }
  },
  {
    path: 'biometric',
    component: BiometricSetupComponent,
    canActivate: [RoleGuard],
    data: { roles: ['SuperAdmin', 'Admin', 'Member'] }
  },
  {
    path: 'devices',
    component: LinkedDevicesComponent,
    canActivate: [RoleGuard],
    data: { roles: ['SuperAdmin', 'Admin', 'Member'] }
  },
  {
    path: 'notifications',
    component: NotificationPreferencesComponent,
    canActivate: [RoleGuard],
    data: { roles: ['SuperAdmin', 'Admin', 'Member'] }
  },
  {
    path: 'my-reports',
    component: MyReportsComponent,
    canActivate: [RoleGuard],
    data: { roles: ['SuperAdmin', 'Admin', 'Member'] }
  }
];

@NgModule({
  imports: [RouterModule.forChild(routes)],
  exports: [RouterModule]
})
export class ProfileRoutingModule { }
