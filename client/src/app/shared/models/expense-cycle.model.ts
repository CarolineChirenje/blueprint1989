export interface CycleMemberDto {
  userId: number;
  firstName: string;
  lastName: string;
  email: string;
}

export interface MemberBalanceDto {
  userId: number;
  firstName: string;
  lastName: string;
  netBalance: number;
}

export interface CycleBalanceDto {
  cycleId: number;
  cycleName: string;
  balances: MemberBalanceDto[];
}

export interface ExpenseCycleSummaryDto {
  id: number;
  name: string;
  startDate: string;
  endDate: string;
  status: string;
  memberCount: number;
  expenseCount: number;
  totalAmount: number;
  createdAt: string;
  groupId: number;
  groupName: string;
}

export interface ExpenseCycleDto {
  id: number;
  name: string;
  startDate: string;
  endDate: string;
  status: 'Draft' | 'Active' | 'Closed';
  splitType: 'Equal' | 'Custom';
  createdByUserId: number;
  createdAt: string;
  members: CycleMemberDto[];
}

export interface MemberObligationDto {
  id: number;
  expenseId: number;
  userId: number;
  firstName: string;
  lastName: string;
  amountOwed: number;
  isSettled: boolean;
  settledAt: string | null;
}

export interface ObligationsSummaryDto {
  totalOwed: number;
  unsettledCount: number;
  activeCycleCount: number;
  soonestCycleName: string | null;
  soonestDueDate: string | null;
}

export interface ExpenseDto {
  id: number;
  expenseCycleId: number;
  title: string;
  amount: number;
  category: string;
  loggedByUserId: number;
  loggedByName: string;
  notes: string | null;
  createdAt: string;
  obligations: MemberObligationDto[];
}

export interface PaymentDto {
  id: number;
  payerId: number;
  payerFirstName: string;
  payerLastName: string;
  payeeId: number;
  payeeFirstName: string;
  payeeLastName: string;
  expenseCycleId: number;
  amount: number;
  status: string;
  notes: string | null;
  createdAt: string;
  confirmedAt: string | null;
}

export const EXPENSE_CATEGORIES = [
  'Rent', 'Utilities', 'Groceries', 'Transport', 'Entertainment', 'Other'
] as const;

// ── Contribution Summary ──────────────────────────────────────────────────────

export interface CycleMemberContributionDto {
  userId: number;
  firstName: string;
  lastName: string;
  shareOwed: number;
  totalPaid: number;
  /** Positive = overpaid, Negative = still owes */
  balance: number;
  isSettled: boolean;
}

export interface CycleContributionSummaryDto {
  cycleId: number;
  cycleName: string;
  totalExpenses: number;
  memberCount: number;
  sharePerMember: number;
  members: CycleMemberContributionDto[];
}

// ── Disputes ──────────────────────────────────────────────────────────────────

export interface ExpenseDisputeDto {
  id: number;
  expenseId: number;
  expenseTitle: string;
  cycleId: number;
  raisedByUserId: number;
  raiserFullName: string;
  reason: string;
  status: 'Pending' | 'Reviewed' | 'Resolved' | 'Rejected';
  adminNotes: string | null;
  createdAt: string;
  updatedAt: string;
}
