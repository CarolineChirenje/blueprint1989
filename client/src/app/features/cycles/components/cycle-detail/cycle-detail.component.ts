import { Component, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { MatDialog } from '@angular/material/dialog';
import { ExpenseCycleService } from '../../../../core/services/expense-cycle.service';
import { ExpenseService } from '../../../../core/services/expense.service';
import { PaymentService } from '../../../../core/services/payment.service';
import { AuthService } from '../../../../core/services/auth.service';
import {
  ExpenseCycleDto,
  ExpenseDto,
  PaymentDto,
  CycleBalanceDto
} from '../../../../shared/models/expense-cycle.model';
import { AddExpenseDialogComponent } from '../add-expense-dialog/add-expense-dialog.component';
import { AddPaymentDialogComponent } from '../add-payment-dialog/add-payment-dialog.component';
import { RespondPaymentDialogComponent } from '../respond-payment-dialog/respond-payment-dialog.component';

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
  loading = true;
  error = '';
  activeTab: 'expenses' | 'payments' | 'balance' | 'members' = 'expenses';
  isAdmin = false;
  currentUserId: number | null = null;

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private cycleService: ExpenseCycleService,
    private expenseService: ExpenseService,
    private paymentService: PaymentService,
    private auth: AuthService,
    private dialog: MatDialog
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
        this.loadBalance();
        this.loading = false;
      },
      error: () => { this.error = 'Cycle not found.'; this.loading = false; }
    });
  }

  loadExpenses(): void {
    if (!this.cycle) return;
    this.expenseService.getByCycle(this.cycle.id).subscribe({
      next: e => this.expenses = e
    });
  }

  loadPayments(): void {
    if (!this.cycle) return;
    this.paymentService.getByCycle(this.cycle.id).subscribe({
      next: p => this.payments = p
    });
  }

  loadBalance(): void {
    if (!this.cycle) return;
    this.cycleService.getBalance(this.cycle.id).subscribe({
      next: b => this.balance = b
    });
  }

  openAddExpense(): void {
    if (!this.cycle) return;
    const ref = this.dialog.open(AddExpenseDialogComponent, {
      width: '480px',
      data: { cycleId: this.cycle.id }
    });
    ref.afterClosed().subscribe(result => { if (result) { this.loadExpenses(); this.loadBalance(); } });
  }

  openAddPayment(): void {
    if (!this.cycle) return;
    const ref = this.dialog.open(AddPaymentDialogComponent, {
      width: '480px',
      data: { cycleId: this.cycle.id, members: this.cycle.members, currentUserId: this.currentUserId }
    });
    ref.afterClosed().subscribe(result => { if (result) { this.loadPayments(); this.loadBalance(); } });
  }

  openRespondPayment(payment: PaymentDto): void {
    const ref = this.dialog.open(RespondPaymentDialogComponent, {
      width: '400px',
      data: { payment }
    });
    ref.afterClosed().subscribe(result => { if (result) { this.loadPayments(); this.loadBalance(); } });
  }

  deleteExpense(id: number): void {
    if (!confirm('Delete this expense?')) return;
    this.expenseService.delete(id).subscribe(() => { this.loadExpenses(); this.loadBalance(); });
  }

  deletePayment(id: number): void {
    if (!confirm('Cancel this payment?')) return;
    this.paymentService.delete(id).subscribe(() => { this.loadPayments(); this.loadBalance(); });
  }

  closeCycle(): void {
    if (!this.cycle || !confirm('Close this cycle? No more expenses can be added.')) return;
    this.cycleService.close(this.cycle.id).subscribe(() => {
      if (this.cycle) this.cycle.status = 'Closed';
    });
  }

  back(): void {
    this.router.navigate(['/cycles']);
  }

  isActive(): boolean { return this.cycle?.status === 'Active'; }

  isPayer(payment: PaymentDto): boolean { return payment.payerId === this.currentUserId; }
  isPayee(payment: PaymentDto): boolean { return payment.payeeId === this.currentUserId; }

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
}
