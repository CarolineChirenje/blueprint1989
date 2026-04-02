import { Component, OnInit } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { AuthService } from '../../../core/services/auth.service';
import { environment } from '../../../../environments/environment';
import { DialogService } from '../../../shared/services/dialog.service';

@Component({
    selector: 'app-mfa-setup',
    templateUrl: './mfa-setup.component.html',
    styleUrls: ['./mfa-setup.component.css'],
    standalone: false
})
export class MfaSetupComponent implements OnInit {
  step: 'hub' | 'intro' | 'scan' | 'verify' | 'backup' | 'complete' | 'status' = 'hub';
  secret: string = '';
  qrCodeUrl: string = '';
  backupCodes: string[] = [];
  verificationForm: FormGroup;
  errorMessage: string = '';
  successMessage: string = '';
  mfaEnabledAt: Date | null = null;
  isDisabling: boolean = false;
  private apiUrl = `${environment.apiUrl}/auth`;

  constructor(
    private fb: FormBuilder,
    private http: HttpClient,
    private auth: AuthService,
    private router: Router,
    private dialogService: DialogService
  ) {
    this.verificationForm = this.fb.group({
      code: ['', [Validators.required, Validators.pattern(/^\d{6}$/)]]
    });
  }

  ngOnInit() {
    // Check if user already has MFA enabled
    const userInfo = this.auth.getUserInfo();
    if (userInfo?.isMfaEnabled) {
      this.step = 'hub';
    } else {
      this.step = 'hub';
    }
  }

  startSetup() {
    this.step = 'scan';
    this.errorMessage = '';
    
    const token = this.auth.getToken();
    const headers = new HttpHeaders({
      'Authorization': `Bearer ${token}`,
      'Content-Type': 'application/json'
    });

    this.http.post<any>(`${this.apiUrl}/mfa/setup`, {}, { headers }).subscribe({
      next: (response) => {
        this.secret = response.secret;
        this.qrCodeUrl = response.qrCode;
        this.backupCodes = response.backupCodes || [];
      },
      error: (err) => {
        this.errorMessage = 'Failed to generate MFA setup. Please try again.';
        this.step = 'intro';
      }
    });
  }

  proceedToVerify() {
    this.step = 'verify';
    this.errorMessage = '';
  }

  verifyCode() {
    if (this.verificationForm.invalid) return;

    const code = this.verificationForm.value.code;
    const token = this.auth.getToken();
    const headers = new HttpHeaders({
      'Authorization': `Bearer ${token}`,
      'Content-Type': 'application/json'
    });

    const payload = {
      secret: this.secret,
      verificationCode: code,
      backupCodes: this.backupCodes
    };

    this.http.post<any>(`${this.apiUrl}/mfa/confirm`, payload, { headers }).subscribe({
      next: (response) => {
        this.step = 'backup';
        this.errorMessage = '';
        this.successMessage = 'MFA enabled successfully. Please save your backup codes in a secure place.';
        
        // Update user info in localStorage with server response
        const userInfo = this.auth.getUserInfo();
        if (userInfo && response) {
          userInfo.isMfaEnabled = response.isMfaEnabled || true;
          userInfo.mfaEnabledAt = response.mfaEnabledAt || new Date().toISOString();
          this.auth.setUserInfo(userInfo);
        }
      },
      error: (err) => {
        console.error('Failed to confirm MFA:', err);
        this.errorMessage = 'Invalid code. Please check your authenticator app and try again.';
      }
    });
  }

  acknowledgeBackupCodes() {
    this.step = 'complete';
  }

  downloadBackupCodes() {
    const text = 'Vitara - Backup Codes\n\n' +
      'Save these codes in a secure place. Each code can only be used once.\n\n' +
      this.backupCodes.join('\n');
    
    const blob = new Blob([text], { type: 'text/plain' });
    const url = window.URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = 'vitara-backup-codes.txt';
    a.click();
    window.URL.revokeObjectURL(url);
  }

  copyBackupCodes() {
    const text = this.backupCodes.join('\n');
    navigator.clipboard.writeText(text).then(() => {
      this.dialogService.alert('Success', 'Backup codes copied to clipboard!').subscribe();
    });
  }

  goBack() {
    this.step = 'hub';
  }

  fetchMfaStatus() {
    const token = this.auth.getToken();
    const headers = new HttpHeaders({
      'Authorization': `Bearer ${token}`,
      'Content-Type': 'application/json'
    });

    // We can get MFA status from the user info stored in localStorage
    // Or we could create a dedicated endpoint. For now, using localStorage is sufficient.
    const userInfo = this.auth.getUserInfo();
    if (userInfo?.mfaEnabledAt) {
      this.mfaEnabledAt = new Date(userInfo.mfaEnabledAt);
    }
  }

  disableMfa() {
    this.dialogService.confirm({
      title: 'Disable Two-Factor Authentication?',
      message: 'Are you sure you want to disable Two-Factor Authentication?<br><br>This will make your account less secure.',
      confirmText: 'Disable',
      cancelText: 'Keep Enabled',
      confirmColor: 'warn'
    }).subscribe(confirmed => {
      if (!confirmed) return;

      this.isDisabling = true;
      this.errorMessage = '';
    
      const token = this.auth.getToken();
      const headers = new HttpHeaders({
        'Authorization': `Bearer ${token}`,
        'Content-Type': 'application/json'
      });

      this.http.post<any>(`${this.apiUrl}/mfa/disable`, {}, { headers }).subscribe({
        next: (response) => {
          this.successMessage = 'Two-Factor Authentication has been disabled successfully.';
          
          // Update user info in localStorage with server response
          const userInfo = this.auth.getUserInfo();
          if (userInfo && response) {
            userInfo.isMfaEnabled = response.isMfaEnabled || false;
            userInfo.mfaEnabledAt = response.mfaEnabledAt || null;
            this.auth.setUserInfo(userInfo);
          }
          
          this.step = 'hub';
          this.isDisabling = false;
          this.mfaEnabledAt = null;
        },
        error: (err) => {
          console.error('Failed to disable MFA:', err);
          this.errorMessage = 'Failed to disable MFA. Please try again.';
          this.isDisabling = false;
        }
      });
    });
  }

  openMfaFlow() {
    const userInfo = this.auth.getUserInfo();
    if (userInfo?.isMfaEnabled) {
      this.step = 'status';
      this.fetchMfaStatus();
    } else {
      this.step = 'intro';
    }
  }

  cancel() {
    this.step = 'hub';
  }
}
