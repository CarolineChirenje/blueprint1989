import { ChangeDetectorRef, Component, Inject } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { ExpenseDisputeService } from '../../../../core/services/expense-dispute.service';
import { ExpenseDto } from '../../../../shared/models/expense-cycle.model';

@Component({
  selector: 'app-dispute-expense-dialog',
  templateUrl: './dispute-expense-dialog.component.html',
  standalone: false
})
export class DisputeExpenseDialogComponent {
  form: FormGroup;
  saving = false;
  error = '';

  constructor(
    private fb: FormBuilder,
    private dialogRef: MatDialogRef<DisputeExpenseDialogComponent>,
    private disputeService: ExpenseDisputeService,
    @Inject(MAT_DIALOG_DATA) public data: { expense: ExpenseDto },
    private cdr: ChangeDetectorRef
  ) {
    this.form = this.fb.group({
      reason: ['', [Validators.required, Validators.maxLength(1000)]]
    });
  }

  submit(): void {
    if (this.form.invalid) return;
    this.saving = true;
    this.error = '';
    this.disputeService.create({
      expenseId: this.data.expense.id,
      reason: this.form.value.reason.trim()
    }).subscribe({
      next: () => this.dialogRef.close(true),
      error: err => {
        this.error = err.error?.message || 'Failed to raise dispute.';
        this.saving = false;
        this.cdr.detectChanges();
      }
    });
  }

  cancel(): void { this.dialogRef.close(false); }
}
