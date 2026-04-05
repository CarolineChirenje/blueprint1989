import { ChangeDetectorRef, Component } from '@angular/core';
import { MatDialogRef } from '@angular/material/dialog';
import { GroupService } from '../../../core/services/group.service';

@Component({
  selector: 'app-join-group-dialog',
  templateUrl: './join-group-dialog.component.html',
  styleUrls: ['./join-group-dialog.component.css'],
  standalone: false
})
export class JoinGroupDialogComponent {
  joinCode = '';
  sending = false;
  error = '';
  successMessage = '';

  constructor(
    private dialogRef: MatDialogRef<JoinGroupDialogComponent>,
    private groupService: GroupService,
    private cdr: ChangeDetectorRef
  ) {}

  submit(): void {
    const code = this.joinCode.trim().toUpperCase();
    if (!code) return;

    this.sending = true;
    this.error = '';
    this.successMessage = '';

    this.groupService.joinByCode({ joinCode: code }).subscribe({
      next: res => {
        this.sending = false;
        this.successMessage = res.message;
        this.cdr.detectChanges();
      },
      error: err => {
        this.sending = false;
        this.error = err.error?.message || 'Failed to submit join request.';
        this.cdr.detectChanges();
      }
    });
  }

  close(): void {
    this.dialogRef.close(!!this.successMessage);
  }
}
