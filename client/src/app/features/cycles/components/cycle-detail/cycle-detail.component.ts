import { ChangeDetectorRef, Component, OnInit, ViewChild } from '@angular/core';
import { MatTableDataSource } from '@angular/material/table';
import { MatPaginator } from '@angular/material/paginator';
import { MatSort } from '@angular/material/sort';
import { ActivatedRoute, Router } from '@angular/router';
import { MatDialog } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';
import { ExpenseCycleService } from '../../../../core/services/expense-cycle.service';
import { ExpenseService } from '../../../../core/services/expense.service';
import { PaymentService } from '../../../../core/services/payment.service';
import { AuthService } from '../../../../core/services/auth.service';
import { ExpenseDisputeService } from '../../../../core/services/expense-dispute.service';
import { GroupService } from '../../../../core/services/group.service';
import { GroupMemberDto } from '../../../../shared/models/group.model';
import {
  ExpenseCycleDto,
  ExpenseDto,
  PaymentDto,
  CycleBalanceDto,
  CycleContributionSummaryDto,
  ExpenseDisputeDto,
  MukandoRoundDto,
  MukandoContributionDto,
  MukandoCycleSummaryDto,
  MukandoRoundActivityDto,
  MukandoSwapRequestDto,
  OptOutRequestDto,
} from '../../../../shared/models/expense-cycle.model';
import { AddExpenseDialogComponent } from '../add-expense-dialog/add-expense-dialog.component';
import { AddPaymentDialogComponent } from '../add-payment-dialog/add-payment-dialog.component';
import { RespondPaymentDialogComponent } from '../respond-payment-dialog/respond-payment-dialog.component';
import { DisputeExpenseDialogComponent, ResolveDisputeDialogComponent } from '../dispute-expense-dialog/dispute-expense-dialog.component';
import { DialogService } from '../../../../shared/services/dialog.service';

@Component({
  selector: 'app-cycle-detail',
  templateUrl: './cycle-detail.component.html',
  styleUrls: ['./cycle-detail.component.css', '../../../../shared/styles/table.css'],
  standalone: false
})
export class CycleDetailComponent implements OnInit {
  cycle: ExpenseCycleDto | null = null;
  expenses: ExpenseDto[] = [];
  payments: PaymentDto[] = [];
  balance: CycleBalanceDto | null = null;
  contributionSummary: CycleContributionSummaryDto | null = null;
  disputes: ExpenseDisputeDto[] = [];
  loading = true;
  error = '';
  reminderSending = false;
  activeTab: 'expenses' | 'payments' | 'summary' | 'disputes' | 'members' | 'rounds' | 'stats' | 'activity' | 'swaps' | 'optouts' | 'order' = 'expenses';
  isAdmin = false;
  currentUserId: number | null = null;
  addableMembers: GroupMemberDto[] = [];
  selectedAddUserId: number | null = null;
  addingMember = false;

  // Mukando state
  rounds: MukandoRoundDto[] = [];
  selectedRound: MukandoRoundDto | null = null;
  mukandoSummary: MukandoCycleSummaryDto | null = null;
  roundActivities: MukandoRoundActivityDto[] = [];
  swapRequests: MukandoSwapRequestDto[] = [];
  optOutRequests: OptOutRequestDto[] = [];
  contributionProofFile: File | null = null;
  contributionReference = '';
  contributionUploading = false;
  contributionUploadError = '';
  contributionFileName = '';
  payoutAmount: number | null = null;
  payoutMethod = '';
  payoutProofFile: File | null = null;
  payoutReference = '';
  payoutUploading = false;
  payoutUploadError = '';
  payoutFileName = '';
  swapTargetUserId: number | null = null;
  optOutReason = '';

  expenseDataSource = new MatTableDataSource<ExpenseDto>([]);
  paymentDataSource = new MatTableDataSource<PaymentDto>([]);
  expenseColumns = ['title', 'amount', 'category', 'loggedByName', 'date', 'actions'];
  paymentColumns = ['parties', 'amount', 'status', 'date', 'actions'];
  expenseFilter = '';
  paymentFilter = '';
  expandedExpense: ExpenseDto | null = null;

  @ViewChild('expensePaginator') set expensePaginatorRef(p: MatPaginator) {
    if (p) this.expenseDataSource.paginator = p;
  }
  @ViewChild('expenseSort') set expenseSortRef(s: MatSort) {
    if (s) this.expenseDataSource.sort = s;
  }
  @ViewChild('paymentPaginator') set paymentPaginatorRef(p: MatPaginator) {
    if (p) this.paymentDataSource.paginator = p;
  }
  @ViewChild('paymentSort') set paymentSortRef(s: MatSort) {
    if (s) this.paymentDataSource.sort = s;
  }

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private cycleService: ExpenseCycleService,
    private expenseService: ExpenseService,
    private paymentService: PaymentService,
    private disputeService: ExpenseDisputeService,
    private auth: AuthService,
    private dialog: MatDialog,
    private snackBar: MatSnackBar,
    private cdr: ChangeDetectorRef,
    private dialogService: DialogService,
    private groupService: GroupService
  ) {}

  ngOnInit(): void {
    this.isAdmin = this.auth.isAdminOrAbove();
    this.currentUserId = this.auth.getUserId();
    const id = Number(this.route.snapshot.paramMap.get('id'));
    const tab = this.route.snapshot.queryParamMap.get('tab');
    if (tab) {
      const validTabs = ['expenses', 'payments', 'summary', 'disputes', 'members', 'rounds', 'stats', 'activity', 'swaps', 'optouts', 'order'];
      if (validTabs.includes(tab)) {
        this.activeTab = tab as typeof this.activeTab;
      }
    }
    this.loadAll(id);
  }

  loadAll(id: number): void {
    this.loading = true;
    this.cycleService.getById(id).subscribe({
      next: cycle => {
        this.cycle = cycle;
        if (cycle.cycleType === 'Mukando') {
          this.activeTab = this.activeTab === 'expenses' || this.activeTab === 'payments' || this.activeTab === 'summary' ? 'rounds' : this.activeTab;
          this.loadRounds();
          this.loadMukandoSummary();
          this.loadSwapRequests();
        } else {
          this.loadExpenses();
          this.loadPayments();
          this.loadContributionSummary();
        }
        this.loadOptOutRequests();
        this.loadDisputes();
        if (this.canManageCycle() && cycle.status === 'Draft') {
          this.loadAddableMembers(cycle);
        }
        this.loading = false;
        this.cdr.detectChanges();
      },
      error: () => { this.error = 'Cycle not found.'; this.loading = false; this.cdr.detectChanges(); }
    });
  }

  loadExpenses(): void {
    if (!this.cycle) return;
    this.expenseService.getByCycle(this.cycle.id).subscribe({
      next: e => {
        this.expenses = e;
        this.expenseDataSource.data = e;
        this.expenseDataSource.filterPredicate = (row, filter) => {
          const f = filter.toLowerCase();
          return row.title.toLowerCase().includes(f)
            || row.category.toLowerCase().includes(f)
            || row.loggedByName.toLowerCase().includes(f);
        };
        this.cdr.detectChanges();
      }
    });
  }

  loadPayments(): void {
    if (!this.cycle) return;
    this.paymentService.getByCycle(this.cycle.id).subscribe({
      next: p => {
        this.payments = p;
        this.paymentDataSource.data = p;
        this.paymentDataSource.filterPredicate = (row, filter) => {
          const f = filter.toLowerCase();
          return `${row.payerFirstName} ${row.payerLastName}`.toLowerCase().includes(f)
            || `${row.payeeFirstName} ${row.payeeLastName}`.toLowerCase().includes(f)
            || row.status.toLowerCase().includes(f)
            || (row.notes ?? '').toLowerCase().includes(f);
        };
        this.cdr.detectChanges();
      }
    });
  }

  loadContributionSummary(): void {
    if (!this.cycle) return;
    this.cycleService.getContributionSummary(this.cycle.id).subscribe({
      next: s => { this.contributionSummary = s; this.cdr.detectChanges(); },
      error: () => {}
    });
  }

  loadDisputes(): void {
    if (!this.cycle) return;
    this.disputeService.getByCycle(this.cycle.id).subscribe({
      next: d => { this.disputes = d; this.cdr.detectChanges(); },
      error: () => {}
    });
  }

  openAddExpense(): void {
    if (!this.cycle) return;
    const ref = this.dialog.open(AddExpenseDialogComponent, {
      width: '480px',
      data: { cycleId: this.cycle.id }
    });
    ref.afterClosed().subscribe(result => {
      if (result) { this.loadExpenses(); this.loadContributionSummary(); }
    });
  }

  openEditExpense(expense: ExpenseDto): void {
    if (!this.cycle) return;
    const ref = this.dialog.open(AddExpenseDialogComponent, {
      width: '480px',
      data: {
        cycleId: this.cycle.id,
        expense: { id: expense.id, title: expense.title, amount: expense.amount, category: expense.category, notes: expense.notes }
      }
    });
    ref.afterClosed().subscribe(result => {
      if (result) { this.loadExpenses(); this.loadContributionSummary(); }
    });
  }

  openAddPayment(): void {
    if (!this.cycle) return;
    const ref = this.dialog.open(AddPaymentDialogComponent, {
      width: '480px',
      data: {
        cycleId: this.cycle.id,
        members: this.cycle.members,
        currentUserId: this.currentUserId,
        cycleCreatedByUserId: this.cycle.createdByUserId
      }
    });
    ref.afterClosed().subscribe(result => {
      if (result) { this.loadPayments(); this.loadContributionSummary(); }
    });
  }

  openRespondPayment(payment: PaymentDto): void {
    const ref = this.dialog.open(RespondPaymentDialogComponent, {
      width: '400px',
      data: { payment }
    });
    ref.afterClosed().subscribe(result => {
      if (result) { this.loadPayments(); this.loadContributionSummary(); }
    });
  }

  openDisputeExpense(expense: ExpenseDto): void {
    const ref = this.dialog.open(DisputeExpenseDialogComponent, {
      width: '480px',
      data: { expense }
    });
    ref.afterClosed().subscribe(result => {
      if (result) this.loadDisputes();
    });
  }

  resolveDispute(dispute: ExpenseDisputeDto, status: string): void {
    const ref = this.dialog.open(ResolveDisputeDialogComponent, {
      width: '480px',
      data: { dispute, status }
    });
    ref.afterClosed().subscribe(result => {
      if (result) this.loadDisputes();
    });
  }

  deleteExpense(id: number): void {
    this.dialogService.confirm({
      title: 'Delete Expense',
      message: 'Delete this expense?',
      confirmText: 'Delete',
      confirmColor: 'warn'
    }).subscribe(confirmed => {
      if (!confirmed) return;
      this.expenseService.delete(id).subscribe(() => {
        this.loadExpenses(); this.loadContributionSummary();
      });
    });
  }

  deletePayment(id: number): void {
    this.dialogService.confirm({
      title: 'Cancel Payment',
      message: 'Cancel this payment?',
      confirmText: 'Cancel Payment',
      confirmColor: 'warn'
    }).subscribe(confirmed => {
      if (!confirmed) return;
      this.paymentService.delete(id).subscribe(() => {
        this.loadPayments(); this.loadContributionSummary();
      });
    });
  }

  startCycle(): void {
    if (!this.cycle) return;
    this.cycleService.start(this.cycle.id).subscribe({
      next: updated => {
        this.cycle = updated;
        this.loadExpenses();
        this.loadContributionSummary();
        this.cdr.detectChanges();
      },
      error: err => this.snackBar.open(err?.error?.message ?? 'Could not start cycle.', 'Dismiss', { duration: 5000 })
    });
  }

  sendReminder(): void {
    if (!this.cycle) return;
    this.reminderSending = true;
    this.cycleService.sendReminder(this.cycle.id).subscribe({
      next: () => {
        this.reminderSending = false;
        this.snackBar.open('Reminders sent to all unsettled members.', 'Dismiss', { duration: 4000 });
        this.cdr.detectChanges();
      },
      error: err => {
        this.reminderSending = false;
        this.snackBar.open(err?.error?.message ?? 'Failed to send reminders.', 'Dismiss', { duration: 5000 });
        this.cdr.detectChanges();
      }
    });
  }

  closeCycle(): void {
    if (!this.cycle) return;
    this.dialogService.confirm({
      title: 'Close Cycle',
      message: 'Close this cycle? No more changes can be made.',
      confirmText: 'Close Cycle',
      confirmColor: 'warn'
    }).subscribe(confirmed => {
      if (!confirmed) return;
      this.cycleService.close(this.cycle!.id).subscribe(() => {
        if (this.cycle) this.cycle = { ...this.cycle, status: 'Closed' };
        this.loadContributionSummary();
        this.cdr.detectChanges();
      });
    });
  }

  back(): void { this.router.navigate(['/cycles']); }

  // ── Mukando helpers ─────────────────────────────────────────────────────

  get isMukando(): boolean { return this.cycle?.cycleType === 'Mukando'; }

  get currencySymbol(): string { return this.cycle?.currencySymbol ?? '$'; }

  loadRounds(): void {
    if (!this.cycle) return;
    this.cycleService.getRounds(this.cycle.id).subscribe({
      next: r => { this.rounds = r; this.cdr.detectChanges(); }
    });
  }

  selectRound(round: MukandoRoundDto): void {
    if (this.selectedRound?.id === round.id) {
      this.selectedRound = null;
      this.roundActivities = [];
      return;
    }
    if (!this.cycle) return;
    this.cycleService.getRoundDetail(this.cycle.id, round.id).subscribe({
      next: r => {
        this.selectedRound = r;
        this.loadRoundActivities(r.id);
        this.cdr.detectChanges();
      }
    });
  }

  /** Reload the currently expanded round without toggling it closed. */
  private refreshSelectedRound(): void {
    if (!this.cycle || !this.selectedRound) return;
    const roundId = this.selectedRound.id;
    this.cycleService.getRoundDetail(this.cycle.id, roundId).subscribe({
      next: r => {
        this.selectedRound = r;
        this.loadRoundActivities(r.id);
        this.cdr.detectChanges();
      }
    });
  }

  loadRoundActivities(roundId: number): void {
    if (!this.cycle) return;
    this.cycleService.getRoundActivity(this.cycle.id, roundId).subscribe({
      next: a => { this.roundActivities = a; this.cdr.detectChanges(); }
    });
  }

  loadMukandoSummary(): void {
    if (!this.cycle) return;
    this.cycleService.getMukandoSummary(this.cycle.id).subscribe({
      next: s => { this.mukandoSummary = s; this.cdr.detectChanges(); },
      error: () => {}
    });
  }

  loadSwapRequests(): void {
    if (!this.cycle) return;
    this.cycleService.getSwapRequests(this.cycle.id).subscribe({
      next: s => { this.swapRequests = s; this.cdr.detectChanges(); },
      error: () => {}
    });
  }

  loadOptOutRequests(): void {
    if (!this.cycle) return;
    this.cycleService.getOptOutRequests(this.cycle.id).subscribe({
      next: r => { this.optOutRequests = r; this.cdr.detectChanges(); },
      error: () => {}
    });
  }

  private readonly MAX_FILE_SIZE = 5 * 1024 * 1024; // 5 MB
  private readonly ALLOWED_TYPES = ['image/jpeg', 'image/png', 'image/webp', 'application/pdf'];

  onContributionFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';
    if (!file) return;

    this.contributionUploadError = '';
    this.contributionFileName = '';
    this.contributionProofFile = null;

    if (file.size > this.MAX_FILE_SIZE) {
      this.contributionUploadError = 'File exceeds the 5 MB limit.';
      return;
    }
    if (!this.ALLOWED_TYPES.includes(file.type)) {
      this.contributionUploadError = 'Only JPG, PNG, WebP, or PDF files are allowed.';
      return;
    }

    this.contributionProofFile = file;
    this.contributionFileName = file.name;
  }

  onPayoutFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';
    if (!file) return;

    this.payoutUploadError = '';
    this.payoutFileName = '';
    this.payoutProofFile = null;

    if (file.size > this.MAX_FILE_SIZE) {
      this.payoutUploadError = 'File exceeds the 5 MB limit.';
      return;
    }
    if (!this.ALLOWED_TYPES.includes(file.type)) {
      this.payoutUploadError = 'Only JPG, PNG, WebP, or PDF files are allowed.';
      return;
    }

    this.payoutProofFile = file;
    this.payoutFileName = file.name;
  }

  recordContribution(roundId: number): void {
    if (!this.cycle || !this.contributionProofFile) return;
    this.contributionUploading = true;
    this.cycleService.recordContribution(this.cycle.id, roundId, {
      reference: this.contributionReference || undefined
    }, this.contributionProofFile).subscribe({
      next: () => {
        this.contributionProofFile = null;
        this.contributionReference = '';
        this.contributionFileName = '';
        this.contributionUploading = false;
        this.snackBar.open('Contribution recorded.', 'OK', { duration: 3000 });
        this.refreshSelectedRound();
        this.loadRounds();
      },
      error: err => {
        this.contributionUploading = false;
        this.snackBar.open(err?.error?.message ?? 'Failed.', 'Dismiss', { duration: 5000 });
      }
    });
  }

  confirmContribution(roundId: number, memberId: number): void {
    if (!this.cycle) return;
    this.cycleService.confirmContribution(this.cycle.id, roundId, memberId).subscribe({
      next: () => {
        this.snackBar.open('Contribution confirmed.', 'OK', { duration: 3000 });
        this.refreshSelectedRound();
        this.loadRounds();
      },
      error: err => this.snackBar.open(err?.error?.message ?? 'Failed.', 'Dismiss', { duration: 5000 })
    });
  }

  recordPayout(roundId: number): void {
    if (!this.cycle || !this.payoutProofFile || !this.payoutAmount || !this.payoutMethod) return;
    this.payoutUploading = true;
    this.cycleService.recordPayout(this.cycle.id, roundId, {
      amountDisbursed: this.payoutAmount,
      paymentMethod: this.payoutMethod,
      reference: this.payoutReference || undefined
    }, this.payoutProofFile).subscribe({
      next: () => {
        this.payoutAmount = null;
        this.payoutMethod = '';
        this.payoutProofFile = null;
        this.payoutReference = '';
        this.payoutFileName = '';
        this.payoutUploading = false;
        this.snackBar.open('Payout recorded.', 'OK', { duration: 3000 });
        this.loadRounds();
        this.loadMukandoSummary();
        this.selectedRound = null;
      },
      error: err => {
        this.payoutUploading = false;
        this.snackBar.open(err?.error?.message ?? 'Failed.', 'Dismiss', { duration: 5000 });
      }
    });
  }

  forceCloseRound(roundId: number): void {
    if (!this.cycle) return;
    this.dialogService.confirm({
      title: 'Force Close Round',
      message: 'Force-close this round? This will mark all pending contributions as missed.',
      confirmText: 'Force Close',
      confirmColor: 'warn'
    }).subscribe(confirmed => {
      if (!confirmed) return;
      this.cycleService.forceCloseRound(this.cycle!.id, roundId).subscribe({
        next: () => {
          this.snackBar.open('Round force-closed.', 'OK', { duration: 3000 });
          this.loadRounds();
          this.loadMukandoSummary();
          this.selectedRound = null;
        },
        error: err => this.snackBar.open(err?.error?.message ?? 'Failed.', 'Dismiss', { duration: 5000 })
      });
    });
  }

  exportCsv(): void {
    if (!this.cycle) return;
    this.cycleService.exportCycleCsv(this.cycle.id).subscribe({
      next: blob => {
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = `${this.cycle!.name}-export.csv`;
        a.click();
        URL.revokeObjectURL(url);
      },
      error: () => this.snackBar.open('Export failed.', 'Dismiss', { duration: 5000 })
    });
  }

  respondSwap(swapId: number, accept: boolean): void {
    if (!this.cycle) return;
    this.cycleService.respondSwapRequest(this.cycle.id, swapId, accept).subscribe({
      next: () => {
        this.snackBar.open(accept ? 'Swap accepted.' : 'Swap declined.', 'OK', { duration: 3000 });
        this.loadSwapRequests();
        this.loadRounds();
      },
      error: err => this.snackBar.open(err?.error?.message ?? 'Failed.', 'Dismiss', { duration: 5000 })
    });
  }

  createSwap(): void {
    if (!this.cycle || !this.swapTargetUserId) return;
    this.cycleService.createSwapRequest(this.cycle.id, this.swapTargetUserId).subscribe({
      next: () => {
        this.swapTargetUserId = null;
        this.snackBar.open('Swap request sent.', 'OK', { duration: 3000 });
        this.loadSwapRequests();
      },
      error: err => this.snackBar.open(err?.error?.message ?? 'Failed.', 'Dismiss', { duration: 5000 })
    });
  }

  cancelSwap(swapId: number): void {
    if (!this.cycle) return;
    this.cycleService.cancelSwapRequest(this.cycle.id, swapId).subscribe({
      next: () => {
        this.snackBar.open('Swap cancelled.', 'OK', { duration: 3000 });
        this.loadSwapRequests();
      },
      error: err => this.snackBar.open(err?.error?.message ?? 'Failed.', 'Dismiss', { duration: 5000 })
    });
  }

  respondOptOut(requestId: number, approve: boolean): void {
    if (!this.cycle) return;
    this.cycleService.respondOptOutRequest(this.cycle.id, requestId, approve).subscribe({
      next: () => {
        this.snackBar.open(approve ? 'Opt-out approved.' : 'Opt-out rejected.', 'OK', { duration: 3000 });
        this.loadOptOutRequests();
        if (approve) { if (this.isMukando) { this.loadRounds(); } this.loadAll(this.cycle!.id); }
      },
      error: err => this.snackBar.open(err?.error?.message ?? 'Failed.', 'Dismiss', { duration: 5000 })
    });
  }

  createOptOut(): void {
    if (!this.cycle || !this.optOutReason.trim()) return;
    this.cycleService.createOptOutRequest(this.cycle.id, this.optOutReason.trim()).subscribe({
      next: () => {
        this.optOutReason = '';
        this.snackBar.open('Opt-out request submitted.', 'OK', { duration: 3000 });
        this.loadOptOutRequests();
      },
      error: err => this.snackBar.open(err?.error?.message ?? 'Failed.', 'Dismiss', { duration: 5000 })
    });
  }

  get swappableMembers(): { userId: number; name: string }[] {
    if (!this.cycle) return [];
    return this.cycle.members
      .filter(m => m.userId !== this.currentUserId)
      .map(m => ({ userId: m.userId, name: `${m.firstName} ${m.lastName}` }));
  }

  contributionStatusClass(status: string): string {
    switch (status) {
      case 'Confirmed': return 'contrib-status-confirmed';
      case 'Paid': return 'contrib-status-paid';
      case 'Missed': return 'contrib-status-missed';
      default: return 'contrib-status-pending';
    }
  }

  roundStatusClass(status: string): string {
    switch (status) {
      case 'Completed': return 'round-completed';
      case 'Active': return 'round-active';
      default: return 'round-pending';
    }
  }

  get activeRound(): MukandoRoundDto | undefined {
    return this.rounds.find(r => r.status === 'Active');
  }

  get completedRoundsCount(): number {
    return this.rounds.filter(r => r.status === 'Completed').length;
  }

  pendingSwapCount(): number {
    return this.swapRequests.filter(s => s.status === 'Pending').length;
  }

  pendingOptOutCount(): number {
    return this.optOutRequests.filter(o => o.status === 'Pending').length;
  }

  isMyContribution(c: MukandoContributionDto): boolean {
    return c.userId === this.currentUserId;
  }

  // ── Payout Order management (Draft Mukando) ────────────────────────

  get payoutOrderFromRounds(): { userId: number; name: string }[] {
    return this.rounds
      .slice()
      .sort((a, b) => a.roundNumber - b.roundNumber)
      .map(r => ({ userId: r.recipientUserId, name: r.recipientName }));
  }

  movePayoutUp(index: number): void {
    const order = this.payoutOrderFromRounds;
    if (index <= 0 || index >= order.length) return;
    const reordered = order.map(o => o.userId);
    [reordered[index - 1], reordered[index]] = [reordered[index], reordered[index - 1]];
    this.savePayoutOrder(reordered);
  }

  movePayoutDown(index: number): void {
    const order = this.payoutOrderFromRounds;
    if (index < 0 || index >= order.length - 1) return;
    const reordered = order.map(o => o.userId);
    [reordered[index], reordered[index + 1]] = [reordered[index + 1], reordered[index]];
    this.savePayoutOrder(reordered);
  }

  private savePayoutOrder(order: number[]): void {
    if (!this.cycle) return;
    this.cycleService.updateMukandoSettings(this.cycle.id, {
      contributionAmount: this.cycle.contributionAmount!,
      frequency: this.cycle.frequency!,
      payoutOrder: order
    }).subscribe({
      next: () => {
        this.snackBar.open('Payout order updated.', 'OK', { duration: 2000 });
        this.loadRounds();
      },
      error: err => this.snackBar.open(err?.error?.message ?? 'Failed to update order.', 'Dismiss', { duration: 5000 })
    });
  }

  deleteCycle(): void {
    if (!this.cycle) return;
    this.dialogService.confirm({
      title: 'Delete Cycle',
      message: `Delete "${this.cycle.name}" and all its data? This cannot be undone.`,
      confirmText: 'Delete',
      confirmColor: 'warn'
    }).subscribe(confirmed => {
      if (!confirmed) return;
      this.cycleService.delete(this.cycle!.id).subscribe({
        next: () => this.router.navigate(['/cycles']),
        error: err => this.snackBar.open(err?.error?.message ?? 'Could not delete cycle.', 'Dismiss', { duration: 5000 })
      });
    });
  }

  isDraft(): boolean  { return this.cycle?.status === 'Draft'; }
  isActive(): boolean { return this.cycle?.status === 'Active'; }
  isClosed(): boolean { return this.cycle?.status === 'Closed'; }

  get currentUserIsGroupAdmin(): boolean {
    return this.cycle?.currentUserGroupRole === 'GroupAdmin';
  }

  canManageCycle(): boolean {
    return this.isAdmin || this.currentUserIsGroupAdmin;
  }

  get isCurrentUserCycleMember(): boolean {
    return this.cycle?.members.some(m => m.userId === this.currentUserId) ?? false;
  }

  isPayer(payment: PaymentDto): boolean { return payment.payerId === this.currentUserId; }
  isPayee(payment: PaymentDto): boolean { return payment.payeeId === this.currentUserId; }

  get totalExpenses(): number {
    return this.expenses.reduce((sum, e) => sum + e.amount, 0);
  }

  get totalPayments(): number {
    return this.payments.reduce((sum, p) => sum + p.amount, 0);
  }

  get confirmedPaymentsTotal(): number {
    return this.payments
      .filter(p => p.status === 'Confirmed')
      .reduce((sum, p) => sum + p.amount, 0);
  }

  applyExpenseFilter(event: Event): void {
    const val = (event.target as HTMLInputElement).value;
    this.expenseFilter = val;
    this.expenseDataSource.filter = val.trim().toLowerCase();
  }

  applyPaymentFilter(event: Event): void {
    const val = (event.target as HTMLInputElement).value;
    this.paymentFilter = val;
    this.paymentDataSource.filter = val.trim().toLowerCase();
  }

  removeCycleMember(userId: number): void {
    if (!this.cycle) return;
    this.dialogService.confirm({
      title: 'Remove Member',
      message: 'Remove this member from the cycle?',
      confirmText: 'Remove',
      confirmColor: 'warn'
    }).subscribe(confirmed => {
      if (!confirmed) return;
      this.cycleService.removeMember(this.cycle!.id, userId).subscribe({
        next: () => this.loadAll(this.cycle!.id),
        error: err => this.snackBar.open(err?.error?.message ?? 'Failed to remove member.', 'Dismiss', { duration: 5000 })
      });
    });
  }

  loadAddableMembers(cycle: ExpenseCycleDto): void {
    this.groupService.getGroupMembers(cycle.groupId).subscribe({
      next: members => {
        const cycleUserIds = new Set(cycle.members.map(m => m.userId));
        this.addableMembers = members.filter(m => m.status === 'Accepted' && !cycleUserIds.has(m.userId));
        this.cdr.detectChanges();
      },
      error: () => {}
    });
  }

  addCycleMember(): void {
    if (!this.cycle || !this.selectedAddUserId) return;
    this.addingMember = true;
    this.cycleService.addMember(this.cycle.id, this.selectedAddUserId).subscribe({
      next: () => {
        this.selectedAddUserId = null;
        this.addingMember = false;
        this.loadAll(this.cycle!.id);
      },
      error: err => {
        this.snackBar.open(err?.error?.message ?? 'Failed to add member.', 'Dismiss', { duration: 5000 });
        this.addingMember = false;
        this.cdr.detectChanges();
      }
    });
  }

  addAllMembers(): void {
    if (!this.cycle || this.addableMembers.length === 0) return;
    this.addingMember = true;
    const userIds = this.addableMembers.map(m => m.userId);
    this.cycleService.addMembersBatch(this.cycle.id, userIds).subscribe({
      next: () => {
        this.addingMember = false;
        this.loadAll(this.cycle!.id);
      },
      error: err => {
        this.snackBar.open(err?.error?.message ?? 'Failed to add members.', 'Dismiss', { duration: 5000 });
        this.addingMember = false;
        this.cdr.detectChanges();
      }
    });
  }

  get poolPerRound(): number {
    if (!this.cycle) return 0;
    return (this.cycle.contributionAmount ?? 0) * ((this.cycle.members?.length ?? 1) - 1);
  }

  goToTab(tab: typeof this.activeTab): void {
    this.activeTab = tab;
    this.cdr.detectChanges();
    // Scroll the tab content into view
    setTimeout(() => {
      document.querySelector('.tab-content')?.scrollIntoView({ behavior: 'smooth', block: 'start' });
    });
  }

  netBalanceClass(net: number): string {
    if (net > 0) return 'balance-positive';
    if (net < 0) return 'balance-negative';
    return 'balance-zero';
  }

  netBalanceLabel(net: number): string {
    if (net > 0) return `owes you $${net.toFixed(2)}`;
    if (net < 0) return `you owe $${Math.abs(net).toFixed(2)}`;
    return 'settled';
  }

  contributionClass(balance: number): string {
    if (balance > 0) return 'contrib-positive';
    if (balance < 0) return 'contrib-negative';
    return 'contrib-zero';
  }

  pendingDisputeCount(): number {
    return this.disputes.filter(d => d.status === 'Pending' || d.status === 'Reviewed').length;
  }

  disputeStatusClass(status: string): string {
    if (status === 'Pending')  return 'dispute-pending';
    if (status === 'Reviewed') return 'dispute-reviewed';
    if (status === 'Resolved') return 'dispute-resolved';
    return 'dispute-rejected';
  }
}
