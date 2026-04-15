import { ChangeDetectorRef, Component, Inject, OnDestroy, OnInit } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { KycDocumentDto, KycIdType, KycService } from '../../../../core/services/kyc.service';
import { KycStatus } from '../../../../shared/models/user.model';

export interface KycReviewDialogData {
  userId: number;
  userName: string;
}

@Component({
  selector: 'app-kyc-review-dialog',
  templateUrl: './kyc-review-dialog.component.html',
  styleUrls: ['./kyc-review-dialog.component.css'],
  standalone: false
})
export class KycReviewDialogComponent implements OnInit, OnDestroy {

  doc: KycDocumentDto | null = null;
  isLoading = false;
  errorMessage = '';
  noSubmission = false;

  docObjectUrl:    string | null = null;
  selfieObjectUrl: string | null = null;
  imagesLoading = false;

  actionPanel: 'approve' | 'reject' | 'bypass' | null = null;
  processing = false;

  rejectForm: FormGroup;
  bypassForm: FormGroup;

  readonly KycIdType = KycIdType;
  readonly KycStatus = KycStatus;

  constructor(
    @Inject(MAT_DIALOG_DATA) public data: KycReviewDialogData,
    private dialogRef: MatDialogRef<KycReviewDialogComponent>,
    private kycService: KycService,
    private fb: FormBuilder,
    private cdr: ChangeDetectorRef
  ) {
    this.rejectForm = this.fb.group({
      rejectionReason: ['', [Validators.required, Validators.maxLength(500)]]
    });
    this.bypassForm = this.fb.group({
      note: ['Admin bypass — no document available', [Validators.required, Validators.maxLength(500)]]
    });
  }

  ngOnInit(): void {
    this.load();
  }

  ngOnDestroy(): void {
    this.revokeObjectUrls();
  }

  private revokeObjectUrls(): void {
    if (this.docObjectUrl)    { URL.revokeObjectURL(this.docObjectUrl);    this.docObjectUrl = null; }
    if (this.selfieObjectUrl) { URL.revokeObjectURL(this.selfieObjectUrl); this.selfieObjectUrl = null; }
  }

  load(): void {
    this.isLoading = true;
    this.errorMessage = '';
    this.kycService.getByUserId(this.data.userId).subscribe({
      next: doc => {
        this.doc = doc;
        this.isLoading = false;
        this.cdr.detectChanges();
        this.loadImages(doc);
      },
      error: err => {
        if (err?.status === 404) {
          this.noSubmission = true;
          this.errorMessage = '';
        } else {
          this.errorMessage = err?.error?.message ?? 'Failed to load KYC document.';
        }
        this.isLoading = false;
        this.cdr.detectChanges();
      }
    });
  }

  private loadImages(doc: KycDocumentDto): void {
    if (!doc.documentFileUrl && !doc.selfieWithIdFileUrl) return;
    this.imagesLoading = true;
    const pending = { doc: !!doc.documentFileUrl, selfie: !!doc.selfieWithIdFileUrl };
    const done = () => { if (!pending.doc && !pending.selfie) { this.imagesLoading = false; this.cdr.detectChanges(); } };

    if (doc.documentFileUrl) {
      this.kycService.downloadAsObjectUrl(doc.documentFileUrl).subscribe({
        next: url => { this.docObjectUrl = url; pending.doc = false; done(); },
        error: ()  => { pending.doc = false; done(); }
      });
    }
    if (doc.selfieWithIdFileUrl) {
      this.kycService.downloadAsObjectUrl(doc.selfieWithIdFileUrl).subscribe({
        next: url => { this.selfieObjectUrl = url; pending.selfie = false; done(); },
        error: ()  => { pending.selfie = false; done(); }
      });
    }
  }

  idTypeLabel(t: KycIdType): string {
    switch (t) {
      case KycIdType.NationalId:      return 'National ID';
      case KycIdType.Passport:        return 'Passport';
      case KycIdType.DriversLicense:  return "Driver's Licence";
      case KycIdType.NoDocument:      return 'No document';
      default:                        return 'Unknown';
    }
  }

  approve(): void {
    if (!this.doc) return;
    this.processing = true;
    this.kycService.review(this.doc.id, true).subscribe({
      next: () => this.dialogRef.close('approved'),
      error: err => {
        this.errorMessage = err?.error?.message ?? 'Approval failed.';
        this.processing = false;
        this.cdr.detectChanges();
      }
    });
  }

  reject(): void {
    if (!this.doc || this.rejectForm.invalid) return;
    this.processing = true;
    const reason = this.rejectForm.value.rejectionReason;
    this.kycService.review(this.doc.id, false, reason).subscribe({
      next: () => this.dialogRef.close('rejected'),
      error: err => {
        this.errorMessage = err?.error?.message ?? 'Rejection failed.';
        this.processing = false;
        this.cdr.detectChanges();
      }
    });
  }

  bypass(): void {
    if (this.bypassForm.invalid) return;
    this.processing = true;
    const note = this.bypassForm.value.note;
    this.kycService.bypass(this.data.userId, note).subscribe({
      next: () => this.dialogRef.close('bypassed'),
      error: err => {
        this.errorMessage = err?.error?.message ?? 'Bypass failed.';
        this.processing = false;
        this.cdr.detectChanges();
      }
    });
  }
}