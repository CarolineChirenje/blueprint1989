import { ChangeDetectorRef, Component, Inject, OnInit, Optional } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../../../environments/environment';
import { ExpenseCycleService } from '../../../../core/services/expense-cycle.service';
import { AuthService } from '../../../../core/services/auth.service';
import { GroupService } from '../../../../core/services/group.service';
import { GroupDto, GroupMemberDto } from '../../../../shared/models/group.model';
import { ExpenseCycleSummaryDto, CurrencyDto } from '../../../../shared/models/expense-cycle.model';

interface UserOption { id: number; firstName: string; lastName: string; email: string; }
interface DialogData { groupId?: number; groupMembers?: GroupMemberDto[]; }

@Component({
  selector: 'app-create-cycle-dialog',
  templateUrl: './create-cycle-dialog.component.html',
  styleUrls: ['./create-cycle-dialog.component.css'],
  standalone: false
})
export class CreateCycleDialogComponent implements OnInit {
  form: FormGroup;
  users: UserOption[] = [];
  groups: GroupDto[] = [];
  currencies: CurrencyDto[] = [];
  sourceCycles: ExpenseCycleSummaryDto[] = [];
  saving = false;
  error = '';
  cycleType: 'Majana' | 'Mukando' = 'Majana';
  payoutOrder: number[] = [];

  /** When opened from a group detail page, these are set and the group selector is hidden. */
  presetGroupId: number | null = null;

  constructor(
    private fb: FormBuilder,
    private dialogRef: MatDialogRef<CreateCycleDialogComponent>,
    private cycleService: ExpenseCycleService,
    private auth: AuthService,
    private http: HttpClient,
    private groupService: GroupService,
    @Optional() @Inject(MAT_DIALOG_DATA) private data: DialogData | null,
    private cdr: ChangeDetectorRef
  ) {
    this.presetGroupId = data?.groupId ?? null;
    this.form = this.fb.group({
      name:               ['', [Validators.required, Validators.maxLength(150)]],
      startDate:          ['', Validators.required],
      endDate:            ['', Validators.required],
      memberIds:          [[], Validators.required],
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

    if (this.data?.groupMembers?.length) {
      this.users = this.data.groupMembers
        .filter(m => m.status === 'Accepted')
        .map(m => ({ id: m.userId, firstName: m.firstName, lastName: m.lastName, email: m.email }));
      this.cdr.detectChanges();
    } else {
      this.http.get<UserOption[]>(`${environment.apiUrl}/auth/users`).subscribe({
        next: users => { this.users = users; this.cdr.detectChanges(); },
        error: () => {}
      });
    }

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

    // Sync payout order when members change
    this.form.get('memberIds')!.valueChanges.subscribe((ids: number[]) => {
      if (this.cycleType === 'Mukando') {
        this.payoutOrder = ids ? [...ids] : [];
      }
    });
  }

  setCycleType(type: 'Majana' | 'Mukando'): void {
    this.cycleType = type;
    if (type === 'Mukando') {
      this.form.get('contributionAmount')!.setValidators([Validators.required, Validators.min(0.01)]);
      this.form.get('frequency')!.setValidators(Validators.required);
      this.form.get('copyFromCycleId')!.setValue(null);
      this.payoutOrder = this.form.value.memberIds ? [...this.form.value.memberIds] : [];
    } else {
      this.form.get('contributionAmount')!.clearValidators();
      this.form.get('frequency')!.clearValidators();
      this.payoutOrder = [];
    }
    this.form.get('contributionAmount')!.updateValueAndValidity();
    this.form.get('frequency')!.updateValueAndValidity();
  }

  movePayoutUp(index: number): void {
    if (index <= 0) return;
    [this.payoutOrder[index - 1], this.payoutOrder[index]] = [this.payoutOrder[index], this.payoutOrder[index - 1]];
  }

  movePayoutDown(index: number): void {
    if (index >= this.payoutOrder.length - 1) return;
    [this.payoutOrder[index], this.payoutOrder[index + 1]] = [this.payoutOrder[index + 1], this.payoutOrder[index]];
  }

  getUserName(userId: number): string {
    const u = this.users.find(u => u.id === userId);
    return u ? `${u.firstName} ${u.lastName}` : `User ${userId}`;
  }

  get mukandoPreview(): { rounds: number; poolPerRound: number; endDate: string } | null {
    const memberCount = this.payoutOrder.length;
    const amount = this.form.value.contributionAmount;
    if (memberCount < 2 || !amount || amount <= 0) return null;
    return {
      rounds: memberCount,
      poolPerRound: amount * (memberCount - 1),
      endDate: '' // calculated from start date + frequency × rounds in backend
    };
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
      endDate: v.endDate,
      memberUserIds: v.memberIds,
      groupId: v.groupId,
      currencyId: v.currencyId,
      cycleType: this.cycleType,
      contributionAmount: this.cycleType === 'Mukando' ? v.contributionAmount : null,
      frequency: this.cycleType === 'Mukando' ? v.frequency : null,
      payoutOrder: this.cycleType === 'Mukando' ? this.payoutOrder : null,
      copyExpensesFromCycleId: this.cycleType === 'Majana' ? (v.copyFromCycleId || null) : null
    }).subscribe({
      next: () => this.dialogRef.close(true),
      error: err => { this.error = err.error?.message || 'Failed to create cycle.'; this.saving = false; }
    });
  }

  cancel(): void { this.dialogRef.close(false); }
}
