import { Component, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';
import { BiometricService, WebAuthnCredentialDto } from '../../../core/services/biometric.service';

type SetupStep = 'list' | 'confirm' | 'waiting' | 'success' | 'error';

@Component({
  selector: 'app-biometric-setup',
  templateUrl: './biometric-setup.component.html',
  styleUrls: ['./biometric-setup.component.css'],
  standalone: false
})
export class BiometricSetupComponent implements OnInit {
  step: SetupStep = 'list';
  credentials: WebAuthnCredentialDto[] = [];
  isSupported = false;
  friendlyName = '';
  errorMessage = '';
  successMessage = '';
  loadingCredentials = true;
  deletingId: number | null = null;

  constructor(
    private biometric: BiometricService,
    private auth: AuthService,
    private router: Router
  ) {}

  ngOnInit(): void {
    this.isSupported = this.biometric.isSupported();
    this.loadCredentials();
  }

  loadCredentials(): void {
    this.loadingCredentials = true;
    this.biometric.getCredentials().subscribe({
      next: creds => {
        this.credentials = creds;
        this.loadingCredentials = false;
      },
      error: () => {
        this.errorMessage = 'Failed to load biometric credentials.';
        this.loadingCredentials = false;
      }
    });
  }

  startAdd(): void {
    this.friendlyName = '';
    this.errorMessage = '';
    this.step = 'confirm';
  }

  cancel(): void {
    this.step = 'list';
    this.errorMessage = '';
  }

  register(): void {
    this.errorMessage = '';
    this.step = 'waiting';
    this.biometric.registerCredential(this.friendlyName || undefined).subscribe({
      next: result => {
        const currentUser = this.auth.getUserInfo();
        if (currentUser?.email) {
          this.biometric.setBiometricHint(currentUser.email);
        }
        this.loadCredentials();
        this.step = 'success';
        this.successMessage = `Biometric login registered${result?.friendlyName ? ' as "' + result.friendlyName + '"' : ''}. You can now log in with your fingerprint or Face ID.`;
      },
      error: err => {
        const message = err?.error ?? err?.message ?? 'Registration failed. Please try again.';
        this.errorMessage = typeof message === 'string' ? message : JSON.stringify(message);
        this.step = 'error';
      }
    });
  }

  deleteCredential(id: number): void {
    if (!confirm('Remove this biometric credential? You will need to re-register to use biometric login on this device.')) return;
    this.deletingId = id;
    this.biometric.deleteCredential(id).subscribe({
      next: () => {
        this.credentials = this.credentials.filter(c => c.id !== id);
        this.deletingId = null;
        // If no credentials remain, clear the hint so the biometric button disappears at login
        if (this.credentials.length === 0) {
          this.biometric.clearBiometricHint();
        }
      },
      error: () => {
        this.errorMessage = 'Failed to remove credential. Please try again.';
        this.deletingId = null;
      }
    });
  }

  goBack(): void {
    this.router.navigate(['/profile']);
  }

  done(): void {
    this.step = 'list';
    this.successMessage = '';
  }

  formatDate(dateStr: string): string {
    if (!dateStr) return 'Unknown';
    return new Date(dateStr).toLocaleDateString(undefined, { year: 'numeric', month: 'short', day: 'numeric' });
  }
}
