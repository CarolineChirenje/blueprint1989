import { Injectable } from '@angular/core';
import { MatDialog, MatDialogRef } from '@angular/material/dialog';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { ConfirmDialogComponent, ConfirmDialogData } from '../components/confirm-dialog/confirm-dialog.component';

@Injectable({
  providedIn: 'root'
})
export class DialogService {
  constructor(private dialog: MatDialog) {}

  /**
   * Opens a confirmation dialog
   * @returns Observable<boolean> - true if confirmed, false/undefined if cancelled
   */
  confirm(data: ConfirmDialogData): Observable<boolean> {
    const isMobile = window.innerWidth < 600;
    const dialogRef: MatDialogRef<ConfirmDialogComponent, boolean> = this.dialog.open(
      ConfirmDialogComponent,
      {
        width: isMobile ? '92vw' : '480px',
        maxWidth: isMobile ? '92vw' : '90vw',
        maxHeight: '90vh',
        position: isMobile ? { bottom: '16px' } : undefined,
        data: data,
        disableClose: false,
        autoFocus: 'dialog',
        panelClass: isMobile ? ['custom-dialog-container', 'mobile-dialog'] : ['custom-dialog-container']
      }
    );

    return dialogRef.afterClosed().pipe(
      map(result => result === true) // Convert undefined to false
    );
  }

  /**
   * Convenience method for delete confirmations
   */
  confirmDelete(itemName: string, customMessage?: string): Observable<boolean> {
    return this.confirm({
      title: 'Confirm Delete',
      message: customMessage || `Are you sure you want to delete <strong>${itemName}</strong>?<br><br>This action cannot be undone.`,
      confirmText: 'Delete',
      cancelText: 'Cancel',
      confirmColor: 'warn'
    });
  }

  /**
   * Convenience method for simple info alerts
   */
  alert(title: string, message: string): Observable<boolean> {
    return this.confirm({
      title: title,
      message: message,
      confirmText: 'OK',
      cancelText: '',
      confirmColor: 'primary'
    });
  }

  /**
   * Convenience method for permission denied alerts
   */
  permissionDenied(message?: string): Observable<boolean> {
    return this.confirm({
      title: 'Permission Denied',
      message: message || 'You do not have permission to perform this action.',
      confirmText: 'OK',
      cancelText: '',
      confirmColor: 'primary'
    });
  }
}
