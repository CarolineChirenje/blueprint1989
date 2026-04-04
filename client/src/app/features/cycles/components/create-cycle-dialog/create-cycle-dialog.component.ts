import { ChangeDetectorRef, Component, Inject, OnInit, Optional } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../../../environments/environment';
import { ExpenseCycleService } from '../../../../core/services/expense-cycle.service';
import { AuthService } from '../../../../core/services/auth.service';
import { GroupService } from '../../../../core/services/group.service';
import { GroupDto, GroupMemberDto } from '../../../../shared/models/group.model';
import { ExpenseCycleSummaryDto } from '../../../../shared/models/expense-cycle.model';

interface UserOption { id: number; firstName: string; lastName: string; email: string; }
interface DialogData { groupId?: number; groupMembers?: GroupMemberDto[]; }

@Component({
  selector: 'app-create-cycle-dialog',
  templateUrl: './create-cycle-dialog.component.html',
  standalone: false
})
export class CreateCycleDialogComponent implements OnInit {
  form: FormGroup;
  users: UserOption[] = [];
  groups: GroupDto[] = [];
  sourceCycles: ExpenseCycleSummaryDto[] = [];
  saving = false;
  error = '';

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
      copyFromCycleId:    [null]
    });
  }

  ngOnInit(): void {
    if (this.data?.groupMembers?.length) {
      // Use group members as the member picker source
      this.users = this.data.groupMembers
        .filter(m => m.status === 'Accepted')
        .map(m => ({ id: m.userId, firstName: m.firstName, lastName: m.lastName, email: m.email }));
      this.cdr.detectChanges();
    } else {
      // Fall back to all users
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
    const { name, startDate, endDate, memberIds, groupId, copyFromCycleId } = this.form.value;
    this.cycleService.create({ name, startDate, endDate, memberUserIds: memberIds, groupId, copyExpensesFromCycleId: copyFromCycleId || null }).subscribe({
      next: () => this.dialogRef.close(true),
      error: err => { this.error = err.error?.message || 'Failed to create cycle.'; this.saving = false; }
    });
  }

  cancel(): void { this.dialogRef.close(false); }
}
