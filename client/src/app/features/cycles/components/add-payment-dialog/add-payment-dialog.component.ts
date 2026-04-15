import { ChangeDetectorRef, Component, Inject } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { PaymentService } from '../../../../core/services/payment.service';

@Component({
  selector: 'app-add-payment-dialog',
  templateUrl: './add-payment-dialog.component.html',
  standalone: false
})
export class AddPaymentDialogComponent {
  form: FormGroup;
  saving = false;
  error = '';

  constructor(
    private fb: FormBuilder,
    private dialogRef: MatDialogRef<AddPaymentDialogComponent>,
    private paymentService: PaymentService,
    @Inject(MAT_DIALOG_DATA) public data: {
      cycleId: number;
      cycleCreatedByUserId: number;
      currentUserId: number | null;
    },
    private cdr: ChangeDetectorRef
  ) {
    this.form = this.fb.group({
      amount: [null, [Validators.required, Validators.min(0.01)]],
      notes:  ['']
    });
  }

  submit(): void {
    if (this.form.invalid) return;
    this.saving = true;
    this.error = '';
    const { amount, notes } = this.form.value;
    this.paymentService.create({
      payeeId: this.data.cycleCreatedByUserId,
      expenseCycleId: this.data.cycleId,
      amount: parseFloat(amount),
      notes: notes || undefined
    }).subscribe({
      next: () => this.dialogRef.close(true),
      error: err => { this.error = err.error?.message || 'Failed to submit payment.'; this.saving = false; this.cdr.detectChanges(); }
    });
  }

  cancel(): void { this.dialogRef.close(false); }

  get isSelfPayment(): boolean {
    return this.data.currentUserId != null && this.data.currentUserId === this.data.cycleCreatedByUserId;
  }
}
