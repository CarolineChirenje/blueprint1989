import { Component, Inject, OnInit, Optional } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { GroupService } from '../../../../core/services/group.service';
import { GroupDto } from '../../../../shared/models/group.model';

interface DialogData { group?: GroupDto; }

@Component({
  selector: 'app-create-group-dialog',
  templateUrl: './create-group-dialog.component.html',
  standalone: false
})
export class CreateGroupDialogComponent implements OnInit {
  form: FormGroup;
  saving = false;
  error = '';
  isEdit: boolean;

  constructor(
    private fb: FormBuilder,
    private dialogRef: MatDialogRef<CreateGroupDialogComponent>,
    private groupService: GroupService,
    @Optional() @Inject(MAT_DIALOG_DATA) public data: DialogData
  ) {
    this.isEdit = !!data?.group;
    this.form = this.fb.group({
      name:        [data?.group?.name ?? '',        [Validators.required, Validators.maxLength(150)]],
      description: [data?.group?.description ?? '', Validators.maxLength(500)],
      isActive:    [data?.group?.isActive ?? true]
    });
  }

  ngOnInit(): void {}

  submit(): void {
    if (this.form.invalid) return;
    this.saving = true;
    this.error = '';
    const { name, description, isActive } = this.form.value;

    const obs = this.isEdit
      ? this.groupService.updateGroup(this.data.group!.id, { name, description, isActive })
      : this.groupService.createGroup({ name, description });

    obs.subscribe({
      next: () => this.dialogRef.close(true),
      error: err => { this.error = err.error?.message || 'Failed to save group.'; this.saving = false; }
    });
  }

  cancel(): void { this.dialogRef.close(false); }
}
