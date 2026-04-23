import { ChangeDetectorRef, Component, OnInit } from '@angular/core';
import {
  PushNotificationService,
  NotificationType,
  TestPushRequest,
  TestEmailRequest
} from '../../core/services/push-notification.service';

interface NotificationTypeOption {
  value: NotificationType;
  label: string;
}

@Component({
  selector: 'app-push-test',
  templateUrl: './push-test.component.html',
  standalone: false
})
export class PushTestComponent implements OnInit {

  // Subscription state
  permission: NotificationPermission = 'default';
  isSubscribed = false;
  swSupported = 'serviceWorker' in navigator && 'PushManager' in window;

  // Test form
  selectedType: NotificationType = NotificationType.General;
  testTitle = 'Blueprint1989 Test';
  testBody = 'This is a test push notification.';
  testDeepLink = '/dashboard';

  // Email test form
  testEmailTo = 'elroitec@gmail.com';
  testEmailSubject = 'Blueprint1989 Test Email';
  testEmailBody = 'This is a test email from Blueprint1989.';
  emailLoading = false;

  // UI state
  status = '';
  statusClass = '';
  loading = false;

  readonly notificationTypes: NotificationTypeOption[] = [
    { value: NotificationType.General,         label: 'General' },
    { value: NotificationType.PaymentDue,      label: 'Payment Due' },
    { value: NotificationType.PaymentReceived, label: 'Payment Received' },
    { value: NotificationType.CycleCreated,    label: 'Cycle Created' },
    { value: NotificationType.SystemRestart,   label: 'System Restart' },
  ];

  constructor(private pushService: PushNotificationService, private cdr: ChangeDetectorRef) {}

  ngOnInit(): void {
    this.pushService.permission$.subscribe(p => { this.permission = p; this.cdr.detectChanges(); });
    this.pushService.isSubscribed$.subscribe(s => { this.isSubscribed = s; this.cdr.detectChanges(); });
  }

  async requestPermission(): Promise<void> {
    this.permission = await this.pushService.requestPermission();
  }

  async subscribe(): Promise<void> {
    this.loading = true;
    this.setStatus('Subscribing…', 'info');
    const ok = await this.pushService.subscribeToServer();
    this.loading = false;
    ok
      ? this.setStatus('Subscribed successfully', 'success')
      : this.setStatus('Subscription failed — check console', 'error');
  }

  async unsubscribe(): Promise<void> {
    this.loading = true;
    this.setStatus('Unsubscribing…', 'info');
    await this.pushService.unsubscribeFromServer();
    this.loading = false;
    this.setStatus('Unsubscribed', 'success');
  }

  sendTest(): void {
    if (!this.testTitle || !this.testBody) {
      this.setStatus('Please fill in title and body.', 'error');
      return;
    }
    const req: TestPushRequest = {
      type: Number(this.selectedType) as NotificationType, // guard against HTML select coercing to string
      title: this.testTitle,
      body: this.testBody,
      deepLinkUrl: this.testDeepLink || undefined
    };
    this.loading = true;
    this.setStatus('Sending…', 'info');
    this.pushService.sendTestPush(req).subscribe({
      next: res => {
        this.loading = false;
        this.setStatus(res.message, 'success');
        this.cdr.detectChanges();
      },
      error: err => {
        this.loading = false;
        const msg = err?.error?.message ?? err?.message ?? 'Unknown error';
        this.setStatus(msg, 'error');
        this.cdr.detectChanges();
      }
    });
  }

  sendTestEmail(): void {
    if (!this.testEmailTo || !this.testEmailSubject || !this.testEmailBody) {
      this.setStatus('Please fill in all email fields.', 'error');
      return;
    }
    const req: TestEmailRequest = {
      to: this.testEmailTo,
      subject: this.testEmailSubject,
      body: this.testEmailBody
    };
    this.emailLoading = true;
    this.setStatus('Sending email…', 'info');
    this.pushService.sendTestEmail(req).subscribe({
      next: res => {
        this.emailLoading = false;
        this.setStatus(res.message, 'success');
        this.cdr.detectChanges();
      },
      error: err => {
        this.emailLoading = false;
        const msg = err?.error?.message ?? err?.message ?? 'Unknown error';
        this.setStatus(msg, 'error');
        this.cdr.detectChanges();
      }
    });
  }

  private setStatus(msg: string, type: 'success' | 'error' | 'info'): void {
    this.status = msg;
    this.statusClass = type;
  }
}
