import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormsModule } from '@angular/forms';
import { SharedModule } from '../shared/shared.module';
import { LoginComponent } from './login.component';
import { SignupComponent } from './signup.component';
import { ChangeExpiredPasswordComponent } from './change-expired-password.component';
import { ForgotPasswordComponent } from './forgot-password.component';
import { ResetPasswordComponent } from './reset-password.component';
import { VerifyEmailComponent } from './verify-email.component';

@NgModule({
  declarations: [
    LoginComponent,
    SignupComponent,
    ChangeExpiredPasswordComponent,
    ForgotPasswordComponent,
    ResetPasswordComponent,
    VerifyEmailComponent
  ],
  imports: [
    CommonModule,
    ReactiveFormsModule,
    FormsModule,
    SharedModule
  ]
})
export class AuthModule { }
