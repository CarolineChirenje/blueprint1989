import { ChangeDetectorRef, Component, Inject } from '@angular/core';
import { FormBuilder, FormControl, FormGroup, Validators } from '@angular/forms';
import { MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { ExpenseDisputeService } from '../../../../core/services/expense-dispute.service';
import { ExpenseDisputeDto, ExpenseDto } from '../../../../shared/models/expense-cycle.model';

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

@Component({
  selector: 'app-resolve-dispute-dialog',
  template: `
    <h2 mat-dialog-title>{{ data.status }} Dispute</h2>
    <mat-dialog-content>
      <p><strong>{{ data.dispute.expenseTitle }}</strong>
        &nbsp;&ndash;&nbsp;raised by {{ data.dispute.raiserFullName }}</p>
      <p class="dispute-reason">{{ data.dispute.reason }}</p>
      <mat-form-field appearance="outline" class="full-width" style="margin-top:12px">
        <mat-label>Admin note (optional)</mat-label>
        <textarea matInput [formControl]="adminNotes" rows="3"
          placeholder="Add a note for the member…"></textarea>
      </mat-form-field>
      @if (error) {
        <p class="error-msg">{{ error }}</p>
      }
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button (click)="cancel()" [disabled]="saving">Cancel</button>
      <button mat-raised-button
              [color]="data.status === 'Rejected' ? 'warn' : 'primary'"
              (click)="submit()" [disabled]="saving">
        {{ saving ? 'Saving…' : data.status }}
      </button>
    </mat-dialog-actions>
  `,
  standalone: false
})
export class ResolveDisputeDialogComponent {
  adminNotes = new FormControl('');
  saving = false;
  error = '';

  constructor(
    private dialogRef: MatDialogRef<ResolveDisputeDialogComponent>,
    private disputeService: ExpenseDisputeService,
    @Inject(MAT_DIALOG_DATA) public data: { dispute: ExpenseDisputeDto; status: string },
    private cdr: ChangeDetectorRef
  ) {}

  submit(): void {
    this.saving = true;
    this.error = '';
    this.disputeService.updateStatus(this.data.dispute.id, {
      status: this.data.status,
      adminNotes: this.adminNotes.value || null
    }).subscribe({
      next: () => this.dialogRef.close(true),
      error: err => {
        this.error = err.error?.message || 'Failed to update dispute.';
        this.saving = false;
        this.cdr.detectChanges();
      }
    });
  }

  cancel(): void { this.dialogRef.close(false); }
}
