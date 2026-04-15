import { ChangeDetectorRef, Component, Inject, OnInit, Optional } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { ExpenseCycleService } from '../../../../core/services/expense-cycle.service';
import { GroupService } from '../../../../core/services/group.service';
import { GroupDto } from '../../../../shared/models/group.model';
import { ExpenseCycleSummaryDto, CurrencyDto } from '../../../../shared/models/expense-cycle.model';

interface DialogData { groupId?: number; }

@Component({
  selector: 'app-create-cycle-dialog',
  templateUrl: './create-cycle-dialog.component.html',
  styleUrls: ['./create-cycle-dialog.component.css'],
  standalone: false
})
export class CreateCycleDialogComponent implements OnInit {
  form: FormGroup;
  groups: GroupDto[] = [];
  currencies: CurrencyDto[] = [];
  sourceCycles: ExpenseCycleSummaryDto[] = [];
  saving = false;
  error = '';
  cycleType: 'Majana' | 'Mukando' = 'Majana';

  /** When opened from a group detail page, these are set and the group selector is hidden. */
  presetGroupId: number | null = null;

  constructor(
    private fb: FormBuilder,
    private dialogRef: MatDialogRef<CreateCycleDialogComponent>,
    private cycleService: ExpenseCycleService,
    private groupService: GroupService,
    @Optional() @Inject(MAT_DIALOG_DATA) private data: DialogData | null,
    private cdr: ChangeDetectorRef
  ) {
    this.presetGroupId = data?.groupId ?? null;
    this.form = this.fb.group({
      name:               ['', [Validators.required, Validators.maxLength(150)]],
      startDate:          ['', Validators.required],
      endDate:            ['', Validators.required],
      groupId:            [this.presetGroupId, Validators.required],
      currencyId:         [null, Validators.required],
      copyFromCycleId:    [null],
      contributionAmount: [null],
      frequency:          [null]
    });
  }

  ngOnInit(): void {
    // Load currencies
    this.cycleService.getCurrencies().subscribe({
      next: currencies => { this.currencies = currencies; this.cdr.detectChanges(); },
      error: () => {}
    });

    if (this.presetGroupId) {
      this.loadSourceCycles(this.presetGroupId);
    } else {
      this.groupService.getGroups().subscribe({
        next: groups => { this.groups = groups; this.cdr.detectChanges(); },
        error: () => {}
      });
      this.form.get('groupId')!.valueChanges.subscribe(gid => {
        this.sourceCycles = [];
        this.form.get('copyFromCycleId')!.setValue(null);
        if (gid) this.loadSourceCycles(gid);
      });
    }


  }

  setCycleType(type: 'Majana' | 'Mukando'): void {
    this.cycleType = type;
    if (type === 'Mukando') {
      this.form.get('contributionAmount')!.setValidators([Validators.required, Validators.min(0.01)]);
      this.form.get('frequency')!.setValidators(Validators.required);
      this.form.get('endDate')!.clearValidators();
      this.form.get('copyFromCycleId')!.setValue(null);
    } else {
      this.form.get('contributionAmount')!.clearValidators();
      this.form.get('frequency')!.clearValidators();
      this.form.get('endDate')!.setValidators(Validators.required);
    }
    this.form.get('contributionAmount')!.updateValueAndValidity();
    this.form.get('frequency')!.updateValueAndValidity();
    this.form.get('endDate')!.updateValueAndValidity();
  }

  loadSourceCycles(groupId: number): void {
    this.cycleService.getAll(groupId).subscribe({
      next: cycles => { this.sourceCycles = cycles; this.cdr.detectChanges(); },
      error: () => {}
    });
  }

  submit(): void {
    if (this.form.invalid) return;
    this.saving = true;
    this.error = '';
    const v = this.form.value;
    this.cycleService.create({
      name: v.name,
      startDate: v.startDate,
      endDate: this.cycleType === 'Mukando'
        ? new Date(new Date(v.startDate).getTime() + 86400000).toISOString().split('T')[0]
        : v.endDate,
      memberUserIds: null,
      groupId: v.groupId,
      currencyId: v.currencyId,
      cycleType: this.cycleType,
      contributionAmount: this.cycleType === 'Mukando' ? v.contributionAmount : null,
      frequency: this.cycleType === 'Mukando' ? v.frequency : null,
      payoutOrder: null,
      copyExpensesFromCycleId: this.cycleType === 'Majana' ? (v.copyFromCycleId || null) : null
    }).subscribe({
      next: (created) => this.dialogRef.close(created.id),
      error: err => { this.error = err.error?.message || 'Failed to create cycle.'; this.saving = false; }
    });
  }

  cancel(): void { this.dialogRef.close(false); }
}
