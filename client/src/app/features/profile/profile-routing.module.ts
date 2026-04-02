import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { ProfileComponent } from './components/profile.component';
import { MfaSetupComponent } from './components/mfa-setup.component';
import { LinkedDevicesComponent } from './components/linked-devices.component';
import { BiometricSetupComponent } from './components/biometric-setup.component';
import { NotificationPreferencesComponent } from './components/notification-preferences/notification-preferences.component';
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
  }
];

@NgModule({
  imports: [RouterModule.forChild(routes)],
  exports: [RouterModule]
})
export class ProfileRoutingModule { }


const routes: Routes = [
  {
    path: '',
    component: ProfileComponent,
    canActivate: [RoleGuard],
    data: { roles: ['Administrator', 'Carer', 'SuperAdmin', 'SupportWorker', 'CareRecipient', 'HealthCareProvider'] }
  },
  {
    path: 'security',
    component: MfaSetupComponent,
    canActivate: [RoleGuard],
    data: { roles: ['Administrator', 'Carer', 'SuperAdmin', 'SupportWorker', 'CareRecipient', 'HealthCareProvider'] }
  },
  {
    path: 'biometric',
    component: BiometricSetupComponent,
    canActivate: [RoleGuard],
    data: { roles: ['Administrator', 'Carer', 'SuperAdmin', 'SupportWorker', 'CareRecipient', 'HealthCareProvider'] }
  },
  {
    path: 'care-recipients',
    component: CareRecipientsComponent,
    canActivate: [RoleGuard],
    data: { roles: ['SupportWorker', 'Carer', 'CareRecipient', 'HealthCareProvider'] }
  },
  {
    path: 'devices',
    component: LinkedDevicesComponent,
    canActivate: [RoleGuard],
    data: { roles: ['Administrator', 'Carer', 'SuperAdmin', 'SupportWorker', 'CareRecipient', 'HealthCareProvider'] }
  },
  {
    path: 'my-conditions',
    component: MyConditionsComponent,
    canActivate: [RoleGuard],
    data: { roles: ['CareRecipient'] }
  },
  {
    path: 'my-reports',
    component: MyReportsComponent,
    canActivate: [RoleGuard],
    data: { roles: ['Administrator', 'Carer', 'SuperAdmin', 'SupportWorker', 'CareRecipient', 'HealthCareProvider'] }
  },
  {
    path: 'notifications',
    component: NotificationPreferencesComponent,
    canActivate: [RoleGuard],
    data: { roles: ['Administrator', 'Carer', 'SuperAdmin', 'SupportWorker', 'CareRecipient', 'HealthCareProvider'] }
  }
];

@NgModule({
  imports: [RouterModule.forChild(routes)],
  exports: [RouterModule]
})
export class ProfileRoutingModule { }
