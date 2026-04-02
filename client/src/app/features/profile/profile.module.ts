import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { SharedModule } from '../../shared/shared.module';
import { ProfileRoutingModule } from './profile-routing.module';
import { ProfileComponent } from './components/profile.component';
import { MfaSetupComponent } from './components/mfa-setup.component';
import { LinkedDevicesComponent } from './components/linked-devices.component';
import { BiometricSetupComponent } from './components/biometric-setup.component';
import { NotificationPreferencesComponent } from './components/notification-preferences/notification-preferences.component';

@NgModule({
  declarations: [
    ProfileComponent,
    MfaSetupComponent,
    LinkedDevicesComponent,
    BiometricSetupComponent,
    NotificationPreferencesComponent
  ],
  imports: [
    CommonModule,
    ReactiveFormsModule,
    FormsModule,
    RouterModule,
    ProfileRoutingModule,
    SharedModule
  ]
})
export class ProfileModule { }


@NgModule({
  declarations: [
    ProfileComponent,
    MfaSetupComponent,
    CareRecipientsComponent,
    LinkedDevicesComponent,
    MyConditionsComponent,
    BiometricSetupComponent,
    MyReportsComponent,
    NotificationPreferencesComponent
  ],
  imports: [
    CommonModule,
    ReactiveFormsModule,
    FormsModule,
    RouterModule,
    ProfileRoutingModule,
    SharedModule
  ]
})
export class ProfileModule { }
