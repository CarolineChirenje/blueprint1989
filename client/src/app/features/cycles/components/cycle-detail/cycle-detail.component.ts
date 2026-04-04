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
  activeTab: 'expenses' | 'payments' | 'summary' | 'disputes' | 'members' = 'expenses';
  isAdmin = false;
  currentUserId: number | null = null;

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
    private dialogService: DialogService
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
    this.disputeService.updateStatus(dispute.id, { status }).subscribe({
      next: () => this.loadDisputes(),
      error: () => {}
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
