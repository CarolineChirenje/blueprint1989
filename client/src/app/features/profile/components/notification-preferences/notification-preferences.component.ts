import { Component, OnInit } from '@angular/core';
import { NotificationPreferenceService } from '../../../../core/services/notification-preference.service';
import {
  NotificationPreferenceDto,
  UpdateNotificationPreferenceItem
} from '../../../../shared/models/notification-preference.model';
import { AudioAlarmService } from '../../../../core/services/audio-alarm.service';

@Component({
  selector: 'app-notification-preferences',
  templateUrl: './notification-preferences.component.html',
  styleUrls: ['./notification-preferences.component.css'],
  standalone: false
})
export class NotificationPreferencesComponent implements OnInit {

  preferences: NotificationPreferenceDto[] = [];
  loading = true;
  saving = false;
  errorMessage = '';
  successMessage = '';

  constructor(
    private prefService: NotificationPreferenceService,
    public audioAlarmService: AudioAlarmService
  ) {}

  ngOnInit(): void {
    this.prefService.getPreferences().subscribe({
      next: prefs => {
        this.preferences = prefs;
        this.loading = false;
      },
      error: () => {
        this.errorMessage = 'Failed to load notification preferences.';
        this.loading = false;
      }
    });
  }

  get healthPrefs(): NotificationPreferenceDto[] {
    return this.preferences.filter(p => p.isAdminControlled);
  }

  get otherPrefs(): NotificationPreferenceDto[] {
    return this.preferences.filter(p => !p.isAdminControlled);
  }

  toggle(pref: NotificationPreferenceDto): void {
    if (pref.isAdminControlled) return;
    pref.isEnabled = !pref.isEnabled;
  }

  save(): void {
    this.saving = true;
    this.errorMessage = '';
    this.successMessage = '';

    const items: UpdateNotificationPreferenceItem[] = this.preferences
      .filter(p => !p.isAdminControlled)
      .map(p => ({ typeId: p.typeId, isEnabled: p.isEnabled }));

    this.prefService.updatePreferences({ preferences: items }).subscribe({
      next: () => {
        this.saving = false;
        this.successMessage = 'Notification preferences saved.';
        setTimeout(() => this.successMessage = '', 3000);
      },
      error: () => {
        this.saving = false;
        this.errorMessage = 'Failed to save preferences. Please try again.';
        setTimeout(() => this.errorMessage = '', 5000);
      }
    });
  }

  /** Returns a friendly label for a type name like "BgTimerReminder" → "BG Timer Reminder". */
  formatName(name: string): string {
    return name.replace(/([A-Z])/g, ' $1').trim();
  }
}
