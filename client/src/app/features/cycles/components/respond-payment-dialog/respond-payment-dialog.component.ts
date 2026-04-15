import { ChangeDetectorRef, Component, Inject } from '@angular/core';
import { FormControl } from '@angular/forms';
import { MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { PaymentService } from '../../../../core/services/payment.service';
import { PaymentDto } from '../../../../shared/models/expense-cycle.model';

@Component({
  selector: 'app-respond-payment-dialog',
  templateUrl: './respond-payment-dialog.component.html',
  standalone: false
})
export class RespondPaymentDialogComponent {
  notes = new FormControl('');
  saving = false;
  error = '';

  constructor(
    private dialogRef: MatDialogRef<RespondPaymentDialogComponent>,
    private paymentService: PaymentService,
    @Inject(MAT_DIALOG_DATA) public data: { payment: PaymentDto },
    private cdr: ChangeDetectorRef
  ) {}

  respond(confirm: boolean): void {
    this.saving = true;
    this.error = '';
    this.paymentService.respond(this.data.payment.id, confirm, this.notes.value || undefined).subscribe({
      next: () => this.dialogRef.close(true),
      error: err => { this.error = err.error?.message || 'Failed to respond to payment.'; this.saving = false; this.cdr.detectChanges(); }
    });
  }

  cancel(): void { this.dialogRef.close(false); }
}
