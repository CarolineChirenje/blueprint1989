import { Component, OnInit } from '@angular/core';
import { AppConfigService } from '../../../shared/services/app-config.service';
import { SystemService } from '../../../shared/services/system.service';
import { AuthService } from '../../../core/services/auth.service';
import { DialogService } from '../../../shared/services/dialog.service';
import { AppConfigEntry, UpdateAppConfigEntryRequest } from '../../../shared/models/app-config.model';

@Component({
  selector: 'app-app-config-management',
  templateUrl: './app-config-management.component.html',
  styleUrls: ['./app-config-management.component.css', '../../../shared/styles/table.css'],
  standalone: false
})
export class AppConfigManagementComponent implements OnInit {
  entries: AppConfigEntry[] = [];
  /** Working copies: key → current edited value */
  editedValues: Record<string, string> = {};
  /** Track which keys have been modified */
  dirtyKeys = new Set<string>();
  /** Keys whose secret value is currently revealed */
  revealedKeys = new Set<string>();

  isLoading = false;
  isSaving = false;
  isRestarting = false;
  successMessage = '';
  errorMessage = '';
  restartWarning = false;

  constructor(
    private appConfigService: AppConfigService,
    private systemService: SystemService,
    private authService: AuthService,
    private dialogService: DialogService
  ) {}

  get canRestart(): boolean {
    return this.authService.isAdmin() || this.authService.isSuperAdmin();
  }

  ngOnInit(): void {
    this.loadEntries();
  }

  loadEntries(): void {
    this.isLoading = true;
    this.appConfigService.getAll().subscribe({
      next: (data) => {
        this.entries = data;
        this.editedValues = {};
        this.dirtyKeys.clear();
        this.revealedKeys.clear();
        data.forEach(e => (this.editedValues[e.key] = e.value));
        this.isLoading = false;
      },
      error: () => {
        this.errorMessage = 'Failed to load configuration settings.';
        this.isLoading = false;
      }
    });
  }

  get categories(): string[] {
    return [...new Set(this.entries.map(e => e.category))];
  }

  entriesForCategory(category: string): AppConfigEntry[] {
    return this.entries.filter(e => e.category === category);
  }

  onValueChange(key: string, newValue: string, originalValue: string): void {
    this.editedValues[key] = String(newValue);
    if (this.editedValues[key] !== originalValue) {
      this.dirtyKeys.add(key);
    } else {
      this.dirtyKeys.delete(key);
    }
  }

  get hasPendingChanges(): boolean {
    return this.dirtyKeys.size > 0;
  }

  toggleReveal(key: string): void {
    if (this.revealedKeys.has(key)) {
      this.revealedKeys.delete(key);
    } else {
      this.revealedKeys.add(key);
    }
  }

  saveAll(): void {
    if (!this.hasPendingChanges) return;

    const updates: UpdateAppConfigEntryRequest[] = [...this.dirtyKeys].map(key => ({
      key,
      value: String(this.editedValues[key])
    }));

    this.restartWarning = updates.some(u => {
      const entry = this.entries.find(e => e.key === u.key);
      return entry?.requiresRestart ?? false;
    });

    this.isSaving = true;
    this.successMessage = '';
    this.errorMessage = '';

    this.appConfigService.bulkUpdate(updates).subscribe({
      next: (response) => {
        this.isSaving = false;

        if (response.hasErrors) {
          this.errorMessage = 'Some settings could not be saved: ' + response.errors.join(', ');
        }

        if (response.updated.length > 0) {
          response.updated.forEach(saved => {
            const idx = this.entries.findIndex(e => e.key === saved.key);
            if (idx !== -1) {
              this.entries[idx] = saved;
              this.editedValues[saved.key] = saved.value;
            }
          });
          this.dirtyKeys.clear();
          this.successMessage = `${response.updated.length} setting(s) saved successfully.`;
        }
      },
      error: () => {
        this.isSaving = false;
        this.errorMessage = 'An error occurred while saving settings. Please try again.';
      }
    });
  }

  resetAll(): void {
    this.dirtyKeys.clear();
    this.entries.forEach(e => (this.editedValues[e.key] = e.value));
    this.successMessage = '';
    this.errorMessage = '';
    this.restartWarning = false;
  }

  restartService(): void {
    this.dialogService.confirm({
      title: 'Restart API Service',
      message: 'Are you sure you want to restart the API service? Active requests will be interrupted.',
      confirmText: 'Restart',
      cancelText: 'Cancel',
      confirmColor: 'warn'
    }).subscribe(confirmed => {
      if (!confirmed) return;

      this.isRestarting = true;
      this.successMessage = '';
      this.errorMessage = '';

      this.systemService.restart().subscribe({
        next: () => {
          this.isRestarting = false;
          this.restartWarning = false;
          this.successMessage = 'Restart initiated. The service will be back shortly.';
        },
        error: () => {
          this.isRestarting = false;
          this.errorMessage = 'Failed to initiate restart. Please try again or restart manually.';
        }
      });
    });
  }

  trackByKey(_: number, entry: AppConfigEntry): string {
    return entry.key;
  }
}
