import { Component, OnInit, OnDestroy, ChangeDetectorRef } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { KycService, KycDocumentDto, KycIdType } from '../../../core/services/kyc.service';
import { KycStatus } from '../../../shared/models/user.model';

@Component({
  selector: 'app-kyc-review',
  templateUrl: './kyc-review.component.html',
  styleUrls: ['./kyc-review.component.css'],
  standalone: false
})
export class KycReviewComponent implements OnInit, OnDestroy {

  items: KycDocumentDto[] = [];
  isLoading = false;
  errorMessage = '';
  successMessage = '';

  /** Currently expanded submission */
  selected: KycDocumentDto | null = null;

  /** Authenticated blob object URLs for the selected submission's images */
  docObjectUrl: string | null = null;
  selfieObjectUrl: string | null = null;
  imagesLoading = false;

  reviewForm: FormGroup;
  bypassForm: FormGroup;

  /** Which action panel is open: 'approve' | 'reject' | 'bypass' | null */
  actionPanel: 'approve' | 'reject' | 'bypass' | null = null;
  processingId: number | null = null;

  readonly KycIdType = KycIdType;
  readonly KycStatus = KycStatus;

  constructor(
    private kycService: KycService,
    private fb: FormBuilder,
    private cdr: ChangeDetectorRef
  ) {
    this.reviewForm = this.fb.group({
      rejectionReason: ['', [Validators.required, Validators.maxLength(500)]]
    });

    this.bypassForm = this.fb.group({
      note: ['', [Validators.required, Validators.maxLength(500)]]
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

  private loadImages(item: KycDocumentDto): void {
    this.revokeObjectUrls();
    if (!item.documentFileUrl && !item.selfieWithIdFileUrl) return;

    this.imagesLoading = true;
    const pending = { doc: !!item.documentFileUrl, selfie: !!item.selfieWithIdFileUrl };
    const done = () => { if (!pending.doc && !pending.selfie) { this.imagesLoading = false; this.cdr.detectChanges(); } };

    if (item.documentFileUrl) {
      this.kycService.downloadAsObjectUrl(item.documentFileUrl).subscribe({
        next: url => { this.docObjectUrl = url; pending.doc = false; done(); },
        error: ()  => { pending.doc = false; done(); }
      });
    }

    if (item.selfieWithIdFileUrl) {
      this.kycService.downloadAsObjectUrl(item.selfieWithIdFileUrl).subscribe({
        next: url => { this.selfieObjectUrl = url; pending.selfie = false; done(); },
        error: ()  => { pending.selfie = false; done(); }
      });
    }
  }

  load(): void {
    this.isLoading = true;
    this.errorMessage = '';
    this.successMessage = '';
    this.kycService.getPending().subscribe({
      next: items => {
        this.items = items;
        this.isLoading = false;
        // If the currently selected item is no longer pending, deselect it
        if (this.selected && !items.find(i => i.id === this.selected!.id)) {
          this.revokeObjectUrls();
          this.selected = null;
          this.actionPanel = null;
        }
        this.cdr.detectChanges();
      },
      error: () => {
        this.errorMessage = 'Failed to load pending KYC submissions.';
        this.isLoading = false;
        this.cdr.detectChanges();
      }
    });
  }

  select(item: KycDocumentDto): void {
    if (this.selected?.id === item.id) {
      this.revokeObjectUrls();
      this.selected = null;
      this.actionPanel = null;
    } else {
      this.selected = item;
      this.actionPanel = null;
      this.reviewForm.reset();
      this.bypassForm.reset();
      this.loadImages(item);
    }
  }

  openAction(panel: 'approve' | 'reject' | 'bypass'): void {
    this.actionPanel = panel;
    this.reviewForm.reset();
    this.bypassForm.reset();
  }

  cancelAction(): void {
    this.actionPanel = null;
  }

  approve(): void {
    if (!this.selected) return;
    this.processingId = this.selected.id;
    this.kycService.review(this.selected.id, true).subscribe({
      next: () => {
        this.successMessage = `KYC approved for ${this.selected!.userName}.`;
        this.processingId = null;
        this.revokeObjectUrls();
        this.selected = null;
        this.actionPanel = null;
        this.cdr.detectChanges();
        this.load();
      },
      error: (err) => {
        this.errorMessage = err?.error?.message ?? 'Approval failed.';
        this.processingId = null;
        this.cdr.detectChanges();
      }
    });
  }

  reject(): void {
    if (!this.selected || this.reviewForm.invalid) return;
    const reason = this.reviewForm.value.rejectionReason;
    this.processingId = this.selected.id;
    this.kycService.review(this.selected.id, false, reason).subscribe({
      next: () => {
        this.successMessage = `KYC rejected for ${this.selected!.userName}.`;
        this.processingId = null;
        this.revokeObjectUrls();
        this.selected = null;
        this.actionPanel = null;
        this.reviewForm.reset();
        this.cdr.detectChanges();
        this.load();
      },
      error: (err) => {
        this.errorMessage = err?.error?.message ?? 'Rejection failed.';
        this.processingId = null;
        this.cdr.detectChanges();
      }
    });
  }

  bypass(): void {
    if (!this.selected || this.bypassForm.invalid) return;
    const note = this.bypassForm.value.note;
    this.processingId = this.selected.id;
    this.kycService.bypass(this.selected.userId, note).subscribe({
      next: () => {
        this.successMessage = `KYC bypassed for ${this.selected!.userName}.`;
        this.processingId = null;
        this.revokeObjectUrls();
        this.selected = null;
        this.actionPanel = null;
        this.bypassForm.reset();
        this.cdr.detectChanges();
        this.load();
      },
      error: (err) => {
        this.errorMessage = err?.error?.message ?? 'Bypass failed.';
        this.processingId = null;
        this.cdr.detectChanges();
      }
    });
  }

  idTypeLabel(t: KycIdType): string {
    switch (t) {
      case KycIdType.NationalId:     return 'National ID';
      case KycIdType.Passport:       return 'Passport';
      case KycIdType.DriversLicense: return "Driver's Licence";
      case KycIdType.NoDocument:     return 'No Document';
      default:                       return 'Unknown';
    }
  }

  dismiss(): void {
    this.errorMessage = '';
    this.successMessage = '';
  }
}
