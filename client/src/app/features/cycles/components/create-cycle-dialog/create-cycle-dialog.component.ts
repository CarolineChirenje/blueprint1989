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
  memberSearch = '';

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

  get filteredUsers(): UserOption[] {
    if (!this.memberSearch) return this.users;
    const q = this.memberSearch.toLowerCase();
    return this.users.filter(u =>
      u.firstName.toLowerCase().includes(q) ||
      u.lastName.toLowerCase().includes(q) ||
      u.email.toLowerCase().includes(q)
    );
  }

  get selectedMemberIds(): number[] {
    return this.form.value.memberIds ?? [];
  }

  isMemberSelected(userId: number): boolean {
    return this.selectedMemberIds.includes(userId);
  }

  toggleMember(userId: number): void {
    const ids = [...this.selectedMemberIds];
    const idx = ids.indexOf(userId);
    if (idx >= 0) ids.splice(idx, 1);
    else ids.push(userId);
    this.form.get('memberIds')!.setValue(ids);
    this.form.get('memberIds')!.markAsTouched();
  }

  toggleAllMembers(): void {
    if (this.selectedMemberIds.length === this.users.length) {
      this.form.get('memberIds')!.setValue([]);
    } else {
      this.form.get('memberIds')!.setValue(this.users.map(u => u.id));
    }
    this.form.get('memberIds')!.markAsTouched();
  }

  get allMembersSelected(): boolean {
    return this.users.length > 0 && this.selectedMemberIds.length === this.users.length;
  }

  get someMembersSelected(): boolean {
    return this.selectedMemberIds.length > 0 && this.selectedMemberIds.length < this.users.length;
  }

  get mukandoPreview(): { rounds: number; poolPerRound: number; endDate: string } | null {
    const ids = this.form.value.memberIds as number[] | null;
    const memberCount = ids?.length ?? 0;
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
      endDate: this.cycleType === 'Mukando'
        ? new Date(new Date(v.startDate).getTime() + 86400000).toISOString().split('T')[0]
        : v.endDate,
      memberUserIds: v.memberIds,
      groupId: v.groupId,
      currencyId: v.currencyId,
      cycleType: this.cycleType,
      contributionAmount: this.cycleType === 'Mukando' ? v.contributionAmount : null,
      frequency: this.cycleType === 'Mukando' ? v.frequency : null,
      payoutOrder: this.cycleType === 'Mukando' ? (v.memberIds as number[]) : null,
      copyExpensesFromCycleId: this.cycleType === 'Majana' ? (v.copyFromCycleId || null) : null
    }).subscribe({
      next: () => this.dialogRef.close(true),
      error: err => { this.error = err.error?.message || 'Failed to create cycle.'; this.saving = false; }
    });
  }

  cancel(): void { this.dialogRef.close(false); }
}
