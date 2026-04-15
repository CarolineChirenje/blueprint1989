import { ChangeDetectorRef, Component, OnInit, OnDestroy } from '@angular/core';
import { Router } from '@angular/router';
import { MatDialog } from '@angular/material/dialog';
import { Subscription } from 'rxjs';
import { filter } from 'rxjs/operators';
import { NotificationDto, NotificationService } from '../../core/services/notification.service';
import { NotificationType } from '../../core/services/push-notification.service';
import { ConfirmDialogComponent, ConfirmDialogData } from '../../shared/components/confirm-dialog/confirm-dialog.component';
import { GroupService } from '../../core/services/group.service';
import { PushMessageService } from '../../core/services/push-message.service';
import { GroupInviteDto, AdminPendingJoinRequestDto } from '../../shared/models/group.model';

type NotificationFilter = 'all' | 'unread' | 'archived';

type NotificationGroup = {
  label: string;
  items: NotificationDto[];
};

@Component({
  selector: 'app-notifications',
  templateUrl: './notifications.component.html',
  styleUrls: ['./notifications.component.css'],
  standalone: false
})
export class NotificationsComponent implements OnInit, OnDestroy {
  readonly pageSize = 25;

  selectedFilter: NotificationFilter = 'all';
  unreadCount = 0;
  notifications: NotificationDto[] = [];
  notificationGroups: NotificationGroup[] = [];
  hasMore = false;
  isLoading = true;
  isLoadingMore = false;
  error = '';

  pendingInvites: GroupInviteDto[] = [];
  inviteError = '';
  inviteResponding = false;

  pendingJoinRequests: AdminPendingJoinRequestDto[] = [];
  joinRequestError = '';
  joinRequestResponding = false;

  private pushSub?: Subscription;

  constructor(
    private notificationService: NotificationService,
    private router: Router,
    private dialog: MatDialog,
    private groupService: GroupService,
    private cdr: ChangeDetectorRef,
    private pushMessage: PushMessageService
  ) {}

  get hasReadNotifications(): boolean {
    return this.notifications.some(notification => notification.isRead && !notification.isArchived);
  }

  ngOnInit(): void {
    this.loadNotifications(true);
    this.loadPendingInvites();
    this.loadPendingJoinRequests();
    // Auto-reload when a join request arrives or another admin responds
    this.pushSub = this.pushMessage.pushReceived$.pipe(
      filter(e => e.notificationType === 33 || e.notificationType === 34 || e.notificationType === 35)
    ).subscribe(() => {
      this.loadPendingJoinRequests();
      this.loadNotifications(true);
    });
  }

  ngOnDestroy(): void {
    this.pushSub?.unsubscribe();
  }

  loadPendingInvites(): void {
    this.groupService.getMyInvites().subscribe({
      next: invites => { this.pendingInvites = invites; this.cdr.detectChanges(); },
      error: () => {}
    });
  }

  respondToInvite(invite: GroupInviteDto, accept: boolean): void {
    this.inviteError = '';
    this.inviteResponding = true;
    this.groupService.respondToInvite(invite.groupId, { accept }).subscribe({
      next: () => { this.inviteResponding = false; this.loadPendingInvites(); this.cdr.detectChanges(); },
      error: err => { this.inviteError = err.error?.message || 'Failed to respond to invite.'; this.inviteResponding = false; this.cdr.detectChanges(); }
    });
  }

  loadPendingJoinRequests(): void {
    this.groupService.getPendingJoinRequestsForAdmin().subscribe({
      next: requests => { this.pendingJoinRequests = requests; this.cdr.detectChanges(); },
      error: () => {}
    });
  }

  respondToJoinRequest(request: AdminPendingJoinRequestDto, approve: boolean): void {
    this.joinRequestError = '';
    this.joinRequestResponding = true;
    this.groupService.respondToJoinRequest(request.groupId, request.userId, { approve }).subscribe({
      next: () => { this.joinRequestResponding = false; this.loadPendingJoinRequests(); this.cdr.detectChanges(); },
      error: err => { this.joinRequestError = err.error?.message || 'Failed to respond to join request.'; this.joinRequestResponding = false; this.cdr.detectChanges(); }
    });
  }

  setFilter(filter: NotificationFilter): void {
    if (this.selectedFilter === filter) {
      return;
    }

    this.selectedFilter = filter;
    this.loadNotifications(true);
  }

  loadNotifications(reset = false): void {
    if (reset) {
      this.isLoading = true;
      this.notifications = [];
      this.notificationGroups = [];
      this.hasMore = false;
    } else {
      this.isLoadingMore = true;
    }

    this.error = '';

    this.notificationService.fetchSummary({
      unreadOnly: this.selectedFilter === 'unread',
      archivedOnly: this.selectedFilter === 'archived',
      take: this.pageSize,
      skip: reset ? 0 : this.notifications.length
    }).subscribe({
      next: summary => {
        this.unreadCount = summary.unreadCount;
        this.notifications = reset
          ? summary.notifications
          : [...this.notifications, ...summary.notifications];
        this.notificationGroups = this.groupNotifications(this.notifications);
        this.hasMore = summary.notifications.length === this.pageSize;
        this.isLoading = false;
        this.isLoadingMore = false;
        this.cdr.detectChanges();
      },
      error: () => {
        this.error = 'Unable to load notifications right now.';
        this.isLoading = false;
        this.isLoadingMore = false;
        this.cdr.detectChanges();
      }
    });
  }

  markRead(notification: NotificationDto, navigateAfter = false): void {
    this.notificationService.markRead(notification.id).subscribe({
      next: () => {
        const wasUnread = !notification.isRead;

        this.notifications = this.selectedFilter === 'unread'
          ? this.notifications.filter(item => item.id !== notification.id)
          : this.notifications.map(item =>
              item.id === notification.id ? { ...item, isRead: true } : item
            );

        if (wasUnread) {
          this.unreadCount = Math.max(0, this.unreadCount - 1);
        }

        this.notificationGroups = this.groupNotifications(this.notifications);

        if (navigateAfter && notification.deepLinkUrl) {
          this.router.navigateByUrl(notification.deepLinkUrl);
        }
        this.cdr.detectChanges();
      },
      error: () => {
        this.error = 'Unable to update that notification.';
        this.cdr.detectChanges();
      }
    });
  }

  markAllRead(): void {
    this.notificationService.markAllRead().subscribe({
      next: () => {
        this.unreadCount = 0;
        this.notifications = this.selectedFilter === 'unread'
          ? []
          : this.notifications.map(notification => ({ ...notification, isRead: true }));
        this.notificationGroups = this.groupNotifications(this.notifications);
        this.hasMore = this.selectedFilter === 'unread' ? false : this.hasMore;
        this.cdr.detectChanges();
      },
      error: () => {
        this.error = 'Unable to mark all notifications as read.';
        this.cdr.detectChanges();
      }
    });
  }

  archiveRead(): void {
    this.notificationService.archiveRead().subscribe({
      next: () => {
        this.notifications = this.notifications.filter(notification => !notification.isRead);
        this.notificationGroups = this.groupNotifications(this.notifications);
        this.cdr.detectChanges();
      },
      error: () => {
        this.error = 'Unable to archive read notifications right now.';
        this.cdr.detectChanges();
      }
    });
  }

  restore(notification: NotificationDto): void {
    this.notificationService.restore(notification.id).subscribe({
      next: () => {
        this.notifications = this.notifications.filter(item => item.id !== notification.id);
        this.notificationGroups = this.groupNotifications(this.notifications);
        this.cdr.detectChanges();
      },
      error: () => {
        this.error = 'Unable to restore that notification right now.';
        this.cdr.detectChanges();
      }
    });
  }

  archiveSingle(notification: NotificationDto): void {
    this.notificationService.archiveSingle(notification.id).subscribe({
      next: () => {
        const wasUnread = !notification.isRead;
        this.notifications = this.notifications.filter(item => item.id !== notification.id);
        if (wasUnread) {
          this.unreadCount = Math.max(0, this.unreadCount - 1);
        }
        this.notificationGroups = this.groupNotifications(this.notifications);
        this.cdr.detectChanges();
      },
      error: () => {
        this.error = 'Unable to archive that notification right now.';
        this.cdr.detectChanges();
      }
    });
  }

  markUnread(notification: NotificationDto): void {
    this.notificationService.markUnread(notification.id).subscribe({
      next: () => {
        if (this.selectedFilter === 'archived') {
          return;
        }
        this.notifications = this.notifications.map(item =>
          item.id === notification.id ? { ...item, isRead: false } : item
        );
        this.unreadCount = this.unreadCount + 1;
        this.notificationGroups = this.groupNotifications(this.notifications);
        this.cdr.detectChanges();
      },
      error: () => {
        this.error = 'Unable to mark that notification as unread.';
        this.cdr.detectChanges();
      }
    });
  }

  deleteNotification(notification: NotificationDto): void {
    const ref = this.dialog.open(ConfirmDialogComponent, {
      data: {
        title: 'Delete notification',
        message: 'This notification will be permanently removed.',
        confirmText: 'Delete',
        cancelText: 'Cancel',
        confirmColor: 'warn'
      } satisfies ConfirmDialogData
    });

    ref.afterClosed().subscribe((confirmed: boolean) => {
      if (!confirmed) return;

      this.notificationService.deleteNotification(notification.id).subscribe({
        next: () => {
          const wasUnread = !notification.isRead && !notification.isArchived;
          this.notifications = this.notifications.filter(item => item.id !== notification.id);
          if (wasUnread) {
            this.unreadCount = Math.max(0, this.unreadCount - 1);
          }
          this.notificationGroups = this.groupNotifications(this.notifications);
          this.cdr.detectChanges();
        },
        error: () => {
          this.error = 'Unable to delete that notification.';
          this.cdr.detectChanges();
        }
      });
    });
  }

  deleteAllArchived(): void {
    const ref = this.dialog.open(ConfirmDialogComponent, {
      data: {
        title: 'Purge archived notifications',
        message: 'All archived notifications will be permanently deleted. This cannot be undone.',
        confirmText: 'Delete all',
        cancelText: 'Cancel',
        confirmColor: 'warn'
      } satisfies ConfirmDialogData
    });

    ref.afterClosed().subscribe((confirmed: boolean) => {
      if (!confirmed) return;

      this.notificationService.deleteAllArchived().subscribe({
        next: () => {
          this.notifications = [];
          this.notificationGroups = [];
          this.hasMore = false;
          this.cdr.detectChanges();
        },
        error: () => {
          this.error = 'Unable to purge archived notifications right now.';
          this.cdr.detectChanges();
        }
      });
    });
  }

  openNotification(notification: NotificationDto): void {
    if (notification.isRead) {
      if (notification.deepLinkUrl) {
        this.router.navigateByUrl(notification.deepLinkUrl);
      }
      return;
    }

    this.markRead(notification, !!notification.deepLinkUrl);
  }

  getNotificationIcon(type: NotificationType): string {
    switch (type) {
      case NotificationType.PaymentDue:
        return '💳';
      case NotificationType.PaymentReceived:
        return '✅';
      case NotificationType.CycleCreated:
        return '🔄';
      case NotificationType.SystemRestart:
        return '🔧';
      case NotificationType.GroupInviteReceived:
        return '👥';
      case NotificationType.CycleStarted:
        return '🚀';
      case NotificationType.CyclePaymentMade:
        return '💸';
      case NotificationType.CycleMidReminder:
        return '⏳';
      case NotificationType.CycleClosingSoon:
        return '⚠️';
      case NotificationType.CycleClosed:
        return '🔒';
      case NotificationType.DisputeRaised:
        return '🚨';
      case NotificationType.DisputeUpdated:
        return '📋';
      case NotificationType.ManualReminder:
        return '📣';
      case NotificationType.MemberLeftGroup:
        return '👋';
      case NotificationType.MemberJoinedGroup:
        return '🎉';
      default:
        return '🔔';
    }
  }

  getNotificationTone(type: NotificationType): 'alert' | 'warning' | 'success' | 'info' | 'neutral' {
    switch (type) {
      case NotificationType.PaymentDue:
        return 'warning';
      case NotificationType.PaymentReceived:
        return 'success';
      case NotificationType.CycleCreated:
        return 'info';
      case NotificationType.SystemRestart:
        return 'info';
      case NotificationType.GroupInviteReceived:
        return 'info';
      case NotificationType.CycleStarted:
        return 'info';
      case NotificationType.CyclePaymentMade:
        return 'success';
      case NotificationType.CycleMidReminder:
        return 'info';
      case NotificationType.CycleClosingSoon:
        return 'warning';
      case NotificationType.CycleClosed:
        return 'neutral';
      case NotificationType.DisputeRaised:
        return 'alert';
      case NotificationType.DisputeUpdated:
        return 'info';
      case NotificationType.ManualReminder:
        return 'warning';
      case NotificationType.MemberLeftGroup:
        return 'info';
      case NotificationType.MemberJoinedGroup:
        return 'info';
      default:
        return 'neutral';
    }
  }

  private groupNotifications(notifications: NotificationDto[]): NotificationGroup[] {
    if (!notifications.length) {
      return [];
    }

    const now = new Date();
    const oneHourAgo = new Date(now.getTime() - 60 * 60 * 1000);
    const startOfToday = new Date(now.getFullYear(), now.getMonth(), now.getDate());
    const groups = new Map<string, NotificationDto[]>();

    for (const notification of notifications) {
      const createdAt = new Date(notification.createdAt);
      let label = 'Earlier';

      if (createdAt >= oneHourAgo) {
        label = 'New';
      } else if (createdAt >= startOfToday) {
        label = 'Today';
      }

      const existing = groups.get(label) ?? [];
      existing.push(notification);
      groups.set(label, existing);
    }

    return ['New', 'Today', 'Earlier']
      .filter(label => groups.has(label))
      .map(label => ({ label, items: groups.get(label)! }));
  }
}
