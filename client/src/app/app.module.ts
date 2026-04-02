import { NgModule } from '@angular/core';
import { BrowserModule } from '@angular/platform-browser';
import { BrowserAnimationsModule } from '@angular/platform-browser/animations';
import { CommonModule } from '@angular/common';
import { provideHttpClient, withInterceptorsFromDi, HTTP_INTERCEPTORS } from '@angular/common/http';
import { ServiceWorkerModule } from '@angular/service-worker';
import { AppRoutingModule } from './app-routing.module';
import { AppComponent } from './app.component';
import { AuthInterceptor } from './core/interceptors/auth.interceptor';
import { TimezoneInterceptor } from './core/interceptors/timezone.interceptor';
import { SharedModule } from './shared/shared.module';
import { AuthModule } from './auth/auth.module';

@NgModule({ declarations: [
        AppComponent
    ],
    bootstrap: [AppComponent], imports: [BrowserModule,
        BrowserAnimationsModule,
        CommonModule,
        AppRoutingModule,
        AuthModule,
        SharedModule,
        ServiceWorkerModule.register('/custom-sw.js', {
          enabled: true,
          // Register immediately so the SW is active when Chrome evaluates
          // PWA installability (beforeinstallprompt fires early in page load).
          registrationStrategy: 'registerImmediately'
        })], providers: [
        provideHttpClient(withInterceptorsFromDi()),
        { provide: HTTP_INTERCEPTORS, useClass: AuthInterceptor, multi: true },
        { provide: HTTP_INTERCEPTORS, useClass: TimezoneInterceptor, multi: true }
    ] })
export class AppModule { }