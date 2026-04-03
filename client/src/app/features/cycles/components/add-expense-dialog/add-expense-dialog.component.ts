import { ChangeDetectorRef, Component, Inject } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { ExpenseService } from '../../../../core/services/expense.service';
import { EXPENSE_CATEGORIES } from '../../../../shared/models/expense-cycle.model';

@Component({
  selector: 'app-add-expense-dialog',
  templateUrl: './add-expense-dialog.component.html',
  standalone: false
})
export class AddExpenseDialogComponent {
  form: FormGroup;
  categories = EXPENSE_CATEGORIES;
  saving = false;
  error = '';

  constructor(
    private fb: FormBuilder,
    private dialogRef: MatDialogRef<AddExpenseDialogComponent>,
    private expenseService: ExpenseService,
    @Inject(MAT_DIALOG_DATA) public data: { cycleId: number },
    private cdr: ChangeDetectorRef
  ) {
    this.form = this.fb.group({
      title:    ['', [Validators.required, Validators.maxLength(200)]],
      amount:   [null, [Validators.required, Validators.min(0.01)]],
      category: ['Other', Validators.required],
      notes:    ['']
    });
  }

  submit(): void {
    if (this.form.invalid) return;
    this.saving = true;
    this.error = '';
    const { title, amount, category, notes } = this.form.value;
    this.expenseService.create({
      expenseCycleId: this.data.cycleId,
      title, amount: parseFloat(amount), category,
      notes: notes || undefined
    }).subscribe({
      next: () => this.dialogRef.close(true),
      error: err => { this.error = err.error?.message || 'Failed to add expense.'; this.saving = false; this.cdr.detectChanges(); }
    });
  }

  cancel(): void { this.dialogRef.close(false); }
}
