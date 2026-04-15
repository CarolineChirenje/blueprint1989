import { ChangeDetectorRef, Component, OnInit } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { forkJoin, of } from 'rxjs';
import { AuthService } from '../../../core/services/auth.service';
import { KycService, KycIdType, KycStatusDto } from '../../../core/services/kyc.service';
import { KycStatus } from '../../../shared/models/user.model';
import { environment } from '../../../../environments/environment';

@Component({
    selector: 'app-profile',
    templateUrl: './profile.component.html',
    styleUrls: ['./profile.component.css'],
    standalone: false
})
export class ProfileComponent implements OnInit {
  profileForm: FormGroup;
  kycForm: FormGroup;
  errorMessage: string = '';
  successMessage: string = '';
  kycStatusInfo: KycStatusDto | null = null;
  kycLoading = false;
  kycSubmitting = false;
  documentFile: File | null = null;
  selfieFile: File | null = null;
  documentFileName = '';
  selfieFileName = '';
  readonly KycStatus = KycStatus;
  readonly KycIdType = KycIdType;
  private apiUrl = `${environment.apiUrl}/auth`;

  constructor(
    private fb: FormBuilder,
    private http: HttpClient,
    public auth: AuthService,
    private kycService: KycService,
    private cdr: ChangeDetectorRef
  ) {
    this.profileForm = this.fb.group({
      email: [{ value: '', disabled: true }],
      role: [{ value: '', disabled: true }],
      firstName: ['', [Validators.required]],
      lastName: ['', [Validators.required]],
      phoneNumber: [''],
      currentPassword: [''],
      newPassword: ['', [Validators.minLength(8), Validators.pattern(/^(?=.*[A-Z])(?=.*[a-z])(?=.*\d).{8,}$/)]],
      confirmPassword: ['']
    });

    this.kycForm = this.fb.group({
      idType: [KycIdType.NationalId, Validators.required],
      idNumber: [''],
      fullNameOnId: ['', Validators.required]
    });
  }

  ngOnInit() {
    this.loadUserProfile();
    this.loadKycStatus();
  }

  loadUserProfile() {
    const user = this.auth.getUserInfo();
    const role = this.auth.getUserRole();
    if (user) {
      this.profileForm.patchValue({
        email: user.email,
        role: role || 'Unknown',
        firstName: user.firstName,
        lastName: user.lastName,
        phoneNumber: user.phoneNumber || ''
      });

      this.kycForm.patchValue({
        fullNameOnId: `${user.firstName ?? ''} ${user.lastName ?? ''}`.trim()
      });
    }
  }

  loadKycStatus() {
    this.kycLoading = true;
    this.kycService.getStatus().subscribe({
      next: status => {
        this.kycStatusInfo = status;
        this.kycLoading = false;
        this.cdr.detectChanges();
      },
      error: () => {
        this.kycLoading = false;
        this.cdr.detectChanges();
      }
    });
  }

  get isNoDocument(): boolean {
    return this.kycForm.get('idType')?.value === KycIdType.NoDocument;
  }

  updateProfile() {
    if (this.profileForm.invalid) {
      this.errorMessage = 'Please fill in all required fields correctly';
      return;
    }

    const { firstName, lastName, phoneNumber, currentPassword, newPassword, confirmPassword } = this.profileForm.value;

    if (newPassword && newPassword !== confirmPassword) {
      this.errorMessage = 'New password and confirmation do not match';
      return;
    }

    if (newPassword && !currentPassword) {
      this.errorMessage = 'Current password is required to change password';
      return;
    }

    const token = this.auth.getToken();
    const headers = new HttpHeaders({
      'Authorization': `Bearer ${token}`,
      'Content-Type': 'application/json'
    });

    const payload: any = { firstName, lastName, phoneNumber };
    if (newPassword) {
      payload.currentPassword = currentPassword;
      payload.newPassword = newPassword;
    }

    this.http.put(`${this.apiUrl}/profile`, payload, { headers }).subscribe({
      next: () => {
        this.successMessage = 'Profile updated successfully';
        this.errorMessage = '';
        const user = this.auth.getUserInfo();
        user.firstName = firstName;
        user.lastName = lastName;
        user.phoneNumber = phoneNumber;
        this.auth.setUserInfo(user);
        this.profileForm.patchValue({ currentPassword: '', newPassword: '', confirmPassword: '' });
        this.cdr.detectChanges();
      },
      error: (err) => {
        this.errorMessage = err.error?.message || 'Failed to update profile';
        this.successMessage = '';
        this.cdr.detectChanges();
      }
    });
  }

  onDocumentSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.documentFile = input.files?.[0] ?? null;
    this.documentFileName = this.documentFile?.name ?? '';
  }

  onSelfieSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.selfieFile = input.files?.[0] ?? null;
    this.selfieFileName = this.selfieFile?.name ?? '';
  }

  submitKyc(): void {
    this.errorMessage = '';
    this.successMessage = '';
    this.kycForm.markAllAsTouched();

    const raw = this.kycForm.getRawValue();
    if (!raw.fullNameOnId?.trim()) {
      this.errorMessage = 'Full name as it appears on your ID is required';
      return;
    }

    if (!this.isNoDocument) {
      if (!raw.idNumber?.trim()) {
        this.errorMessage = 'ID number is required';
        return;
      }
      if (!this.documentFile) {
        this.errorMessage = 'Please attach a photo of the identity document';
        return;
      }
    }

    this.kycSubmitting = true;

    const documentUpload$ = this.documentFile ? this.kycService.uploadFile(this.documentFile, 'kyc') : of(null);
    const selfieUpload$ = this.selfieFile ? this.kycService.uploadFile(this.selfieFile, 'kyc') : of(null);

    forkJoin({ documentFileId: documentUpload$, selfieWithIdFileId: selfieUpload$ }).subscribe({
      next: ({ documentFileId, selfieWithIdFileId }) => {
        this.kycService.submit({
          idType: raw.idType,
          idNumber: this.isNoDocument ? null : raw.idNumber?.trim(),
          fullNameOnId: raw.fullNameOnId.trim(),
          documentFileId,
          selfieWithIdFileId
        }).subscribe({
          next: () => {
            this.successMessage = 'KYC details submitted successfully. An admin will review them shortly.';
            this.kycSubmitting = false;
            this.documentFile = null;
            this.selfieFile = null;
            this.documentFileName = '';
            this.selfieFileName = '';
            this.loadKycStatus();
            this.cdr.detectChanges();
          },
          error: err => {
            this.errorMessage = err?.error?.message || 'Failed to submit KYC details';
            this.kycSubmitting = false;
            this.cdr.detectChanges();
          }
        });
      },
      error: err => {
        this.errorMessage = err?.error?.message || err?.message || 'Failed to upload KYC files';
        this.kycSubmitting = false;
        this.cdr.detectChanges();
      }
    });
  }

  kycStatusLabel(status?: KycStatus | null): string {
    switch (status) {
      case KycStatus.Verified: return 'Verified';
      case KycStatus.PendingReview: return 'Pending Review';
      case KycStatus.Rejected: return 'Rejected';
      case KycStatus.AdminBypassed: return 'Admin Bypassed';
      default: return 'Not Started';
    }
  }

  kycStatusClass(status?: KycStatus | null): string {
    switch (status) {
      case KycStatus.Verified: return 'kyc-verified';
      case KycStatus.PendingReview: return 'kyc-pending';
      case KycStatus.Rejected: return 'kyc-rejected';
      case KycStatus.AdminBypassed: return 'kyc-bypassed';
      default: return 'kyc-not-started';
    }
  }
}
