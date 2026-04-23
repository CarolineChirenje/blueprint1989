import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { AuthGuard } from './core/guards/auth.guard';
import { LoginComponent } from './auth/login.component';
import { SignupComponent } from './auth/signup.component';
import { ChangeExpiredPasswordComponent } from './auth/change-expired-password.component';
import { ForgotPasswordComponent } from './auth/forgot-password.component';
import { ResetPasswordComponent } from './auth/reset-password.component';
import { VerifyEmailComponent } from './auth/verify-email.component';

const routes: Routes = [
  // Auth routes - explicit to avoid wildcard matching
  { path: 'login', component: LoginComponent },
  { path: 'signup', component: SignupComponent },
  { path: 'change-expired-password', component: ChangeExpiredPasswordComponent },
  { path: 'forgot-password', component: ForgotPasswordComponent },
  { path: 'reset-password', component: ResetPasswordComponent },
  { path: 'verify-email', component: VerifyEmailComponent },
  
  // Feature routes with auth guard
  {
    path: 'dashboard',
    loadChildren: () => import('./features/dashboard/dashboard.module').then(m => m.DashboardModule),
    canActivate: [AuthGuard]
  },
  {
    path: 'profile',
    loadChildren: () => import('./features/profile/profile.module').then(m => m.ProfileModule),
    canActivate: [AuthGuard]
  },
  {
    path: 'notifications',
    loadChildren: () => import('./features/notifications/notifications.module').then(m => m.NotificationsModule),
    canActivate: [AuthGuard]
  },
  {
    path: 'release-notes',
    loadChildren: () => import('./features/release-notes/release-notes.module').then(m => m.ReleaseNotesModule)
  },
  {
    path: 'about',
    loadChildren: () => import('./features/about/about.module').then(m => m.AboutModule)
  },
  {
    path: 'dev/push-test',
    loadChildren: () => import('./features/push-test/push-test.module').then(m => m.PushTestModule),
    canActivate: [AuthGuard]
  },
  {
    path: 'offline-queue',
    loadChildren: () => import('./features/offline-queue/offline-queue.module').then(m => m.OfflineQueueModule),
    canActivate: [AuthGuard]
  },
  {
    path: 'management',
    loadChildren: () => import('./features/management/management.module').then(m => m.ManagementModule),
    canActivate: [AuthGuard]
  },
  { 
    path: 'admin', 
    redirectTo: '/dashboard',
    pathMatch: 'full'
  },
  { 
    path: '', 
    redirectTo: '/dashboard',
    pathMatch: 'full'
  },
  // Wildcard at the end
  { path: '**', redirectTo: '/dashboard' }
];

@NgModule({
  imports: [RouterModule.forRoot(routes, { anchorScrolling: 'enabled', scrollPositionRestoration: 'enabled' })],
  exports: [RouterModule] 
})
export class AppRoutingModule { }