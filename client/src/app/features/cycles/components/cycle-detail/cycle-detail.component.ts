import { ChangeDetectorRef, Component, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { MatDialog } from '@angular/material/dialog';
import { ExpenseCycleService } from '../../../../core/services/expense-cycle.service';
import { ExpenseService } from '../../../../core/services/expense.service';
import { PaymentService } from '../../../../core/services/payment.service';
import { AuthService } from '../../../../core/services/auth.service';
import { ExpenseDisputeService } from '../../../../core/services/expense-dispute.service';
import {
  ExpenseCycleDto,
  ExpenseDto,
  PaymentDto,
  CycleBalanceDto,
  CycleContributionSummaryDto,
  ExpenseDisputeDto
} from '../../../../shared/models/expense-cycle.model';
import { AddExpenseDialogComponent } from '../add-expense-dialog/add-expense-dialog.component';
import { AddPaymentDialogComponent } from '../add-payment-dialog/add-payment-dialog.component';
import { RespondPaymentDialogComponent } from '../respond-payment-dialog/respond-payment-dialog.component';
import { DisputeExpenseDialogComponent } from '../dispute-expense-dialog/dispute-expense-dialog.component';

@Component({
  selector: 'app-cycle-detail',
  templateUrl: './cycle-detail.component.html',
  styleUrls: ['./cycle-detail.component.css'],
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
  activeTab: 'expenses' | 'payments' | 'summary' | 'disputes' | 'members' = 'expenses';
  isAdmin = false;
  currentUserId: number | null = null;

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private cycleService: ExpenseCycleService,
    private expenseService: ExpenseService,
    private paymentService: PaymentService,
    private disputeService: ExpenseDisputeService,
    private auth: AuthService,
    private dialog: MatDialog,
    private cdr: ChangeDetectorRef
  ) {}

  ngOnInit(): void {
    this.isAdmin = this.auth.isAdminOrAbove();
    this.currentUserId = this.auth.getUserId();
    const id = Number(this.route.snapshot.paramMap.get('id'));
    this.loadAll(id);
  }

  loadAll(id: number): void {
    this.loading = true;
    this.cycleService.getById(id).subscribe({
      next: cycle => {
        this.cycle = cycle;
        this.loadExpenses();
        this.loadPayments();
        this.loadContributionSummary();
        this.loadDisputes();
        this.loading = false;
        this.cdr.detectChanges();
      },
      error: () => { this.error = 'Cycle not found.'; this.loading = false; this.cdr.detectChanges(); }
    });
  }

  loadExpenses(): void {
    if (!this.cycle) return;
    this.expenseService.getByCycle(this.cycle.id).subscribe({
      next: e => { this.expenses = e; this.cdr.detectChanges(); }
    });
  }

  loadPayments(): void {
    if (!this.cycle) return;
    this.paymentService.getByCycle(this.cycle.id).subscribe({
      next: p => { this.payments = p; this.cdr.detectChanges(); }
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
    this.disputeService.updateStatus(dispute.id, { status }).subscribe({
      next: () => this.loadDisputes(),
      error: () => {}
    });
  }

  deleteExpense(id: number): void {
    if (!confirm('Delete this expense?')) return;
    this.expenseService.delete(id).subscribe(() => {
      this.loadExpenses(); this.loadContributionSummary();
    });
  }

  deletePayment(id: number): void {
    if (!confirm('Cancel this payment?')) return;
    this.paymentService.delete(id).subscribe(() => {
      this.loadPayments(); this.loadContributionSummary();
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
      error: err => alert(err?.error?.message ?? 'Could not start cycle.')
    });
  }

  sendReminder(): void {
    if (!this.cycle) return;
    this.reminderSending = true;
    this.cycleService.sendReminder(this.cycle.id).subscribe({
      next: () => {
        this.reminderSending = false;
        alert('Reminders sent to all unsettled members.');
        this.cdr.detectChanges();
      },
      error: err => {
        this.reminderSending = false;
        alert(err?.error?.message ?? 'Failed to send reminders.');
        this.cdr.detectChanges();
      }
    });
  }

  closeCycle(): void {
    if (!this.cycle || !confirm('Close this cycle? No more changes can be made.')) return;
    this.cycleService.close(this.cycle.id).subscribe(() => {
      if (this.cycle) this.cycle = { ...this.cycle, status: 'Closed' };
      this.loadContributionSummary();
      this.cdr.detectChanges();
    });
  }

  back(): void { this.router.navigate(['/cycles']); }

  isDraft(): boolean  { return this.cycle?.status === 'Draft'; }
  isActive(): boolean { return this.cycle?.status === 'Active'; }
  isClosed(): boolean { return this.cycle?.status === 'Closed'; }

  canManageCycle(): boolean {
    return this.isAdmin || this.cycle?.createdByUserId === this.currentUserId;
  }

  isPayer(payment: PaymentDto): boolean { return payment.payerId === this.currentUserId; }
  isPayee(payment: PaymentDto): boolean { return payment.payeeId === this.currentUserId; }

  removeCycleMember(userId: number): void {
    if (!this.cycle) return;
    if (!confirm('Remove this member from the cycle?')) return;
    this.cycleService.removeMember(this.cycle.id, userId).subscribe({
      next: () => this.loadAll(this.cycle!.id),
      error: err => alert(err?.error?.message ?? 'Failed to remove member.')
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
