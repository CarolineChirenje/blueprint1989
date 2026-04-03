import { ChangeDetectorRef, Component, OnInit } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { MatDialogRef } from '@angular/material/dialog';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../../../environments/environment';
import { ExpenseCycleService } from '../../../../core/services/expense-cycle.service';
import { AuthService } from '../../../../core/services/auth.service';
import { GroupService } from '../../../../core/services/group.service';
import { GroupDto } from '../../../../shared/models/group.model';

interface UserOption { id: number; firstName: string; lastName: string; email: string; }

@Component({
  selector: 'app-create-cycle-dialog',
  templateUrl: './create-cycle-dialog.component.html',
  standalone: false
})
export class CreateCycleDialogComponent implements OnInit {
  form: FormGroup;
  users: UserOption[] = [];
  groups: GroupDto[] = [];
  saving = false;
  error = '';

  constructor(
    private fb: FormBuilder,
    private dialogRef: MatDialogRef<CreateCycleDialogComponent>,
    private cycleService: ExpenseCycleService,
    private auth: AuthService,
    private http: HttpClient,
    private groupService: GroupService,
    private cdr: ChangeDetectorRef
  ) {
    this.form = this.fb.group({
      name:      ['', [Validators.required, Validators.maxLength(150)]],
      startDate: ['', Validators.required],
      endDate:   ['', Validators.required],
      memberIds: [[], Validators.required],
      groupId:   [null, Validators.required]
    });
  }

  ngOnInit(): void {
    this.http.get<UserOption[]>(`${environment.apiUrl}/auth/users`).subscribe({
      next: users => { this.users = users; this.cdr.detectChanges(); },
      error: () => {}
    });
    this.groupService.getGroups().subscribe({
      next: groups => { this.groups = groups; this.cdr.detectChanges(); },
      error: () => {}
    });
  }

  submit(): void {
    if (this.form.invalid) return;
    this.saving = true;
    this.error = '';
    const { name, startDate, endDate, memberIds, groupId } = this.form.value;
    this.cycleService.create({ name, startDate, endDate, memberUserIds: memberIds, groupId }).subscribe({
      next: () => this.dialogRef.close(true),
      error: err => { this.error = err.error?.message || 'Failed to create cycle.'; this.saving = false; }
    });
  }

  cancel(): void { this.dialogRef.close(false); }
}
