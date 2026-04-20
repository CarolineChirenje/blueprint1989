import { KycStatus } from './user.model';

export type CycleRole = 'Participant' | 'Observer';

export interface CycleMemberDto {
  userId: number;
  firstName: string;
  lastName: string;
  email: string;
  groupRole: 'GroupAdmin' | 'GroupMember';
  kycStatus: KycStatus;
  cycleRole: CycleRole;
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

export type CycleType = 'Majana' | 'Mukando';

export interface CurrencyDto {
  id: number;
  code: string;
  name: string;
  symbol: string;
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
  currentUserGroupRole: 'GroupAdmin' | 'GroupMember';
  cycleType: CycleType;
  currencyCode: string;
  currencySymbol: string;
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
  currentUserGroupRole: 'GroupAdmin' | 'GroupMember';
  groupId: number;
  cycleType: CycleType;
  currencyId: number;
  currencyCode: string;
  currencySymbol: string;
  contributionAmount: number | null;
  frequency: string | null;
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
  /** Sum of absolute balances for all unsettled members. */
  totalOutstanding: number;
}

// ── Outstanding Summary (cross-cycle) ────────────────────────────────────────

export interface OutstandingSummaryDto {
  totalOutstanding: number;
  cycleCount: number;
  cycles: CycleOutstandingItemDto[];
}

export interface CycleOutstandingItemDto {
  cycleId: number;
  cycleName: string;
  outstanding: number;
  sharePerMember: number;
  totalPaid: number;
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

// ── Mukando Types ─────────────────────────────────────────────────────────────

export interface MukandoRoundDto {
  id: number;
  roundNumber: number;
  recipientUserId: number;
  recipientName: string;
  status: 'Pending' | 'Active' | 'Completed';
  expectedPool: number;
  actualCollected: number;
  dueDate: string;
  payoutConfirmed: boolean;
  contributions: MukandoContributionDto[] | null;
}

export interface MukandoContributionDto {
  id: number;
  userId: number;
  firstName: string;
  lastName: string;
  amount: number;
  status: 'Pending' | 'Paid' | 'Confirmed' | 'Missed' | 'AwaitingVerification';
  proofUrl: string | null;
  reference: string | null;
  paidAt: string | null;
  confirmedByAdminAt: string | null;
}

export interface MukandoPayoutDto {
  id: number;
  recipientUserId: number;
  recipientName: string;
  amountDisbursed: number;
  paymentMethod: string;
  proofUrl: string;
  reference: string | null;
  confirmedByUserId: number;
  createdAt: string;
}

export interface MukandoSwapRequestDto {
  id: number;
  requesterUserId: number;
  requesterName: string;
  requesterRoundNumber: number;
  targetUserId: number;
  targetName: string;
  targetRoundNumber: number;
  status: 'Pending' | 'Accepted' | 'Declined' | 'Cancelled';
  createdAt: string;
  respondedAt: string | null;
}

export interface OptOutRequestDto {
  id: number;
  userId: number;
  userName: string;
  reason: string;
  status: 'Pending' | 'Approved' | 'Rejected';
  createdAt: string;
  respondedAt: string | null;
}

export interface MukandoRoundActivityDto {
  id: number;
  action: string;
  details: string;
  userId: number;
  userName: string;
  createdAt: string;
}

export interface MukandoCycleSummaryDto {
  cycleId: number;
  cycleName: string;
  totalDisbursed: number;
  totalCollected: number;
  totalExpectedPool: number;
  roundsCompleted: number;
  totalRounds: number;
  onTimeContributionRate: number;
  memberReliability: MemberReliabilityDto[];
}

export interface MemberReliabilityDto {
  userId: number;
  firstName: string;
  lastName: string;
  onTimeCount: number;
  missedCount: number;
  totalContributions: number;
  reliabilityPercent: number;
}

export interface MukandoDashboardDto {
  nextContribution: MukandoNextContributionDto | null;
  payoutRound: MukandoPayoutRoundDto | null;
  activeRoundStatus: MukandoActiveRoundStatusDto | null;
}

export interface MukandoNextContributionDto {
  cycleId: number;
  cycleName: string;
  amount: number;
  currencySymbol: string;
  dueDate: string;
  roundNumber: number;
  recipientName: string;
}

export interface MukandoPayoutRoundDto {
  cycleId: number;
  cycleName: string;
  roundNumber: number;
  estimatedDate: string;
}

export interface MukandoActiveRoundStatusDto {
  cycleId: number;
  cycleName: string;
  roundNumber: number;
  contributionsConfirmed: number;
  contributionsTotal: number;
}

// ── Mukando Verification ──────────────────────────────────────────────────────

export type VerificationTarget = 'Contribution' | 'Payout';
export type VerificationStatus = 'Pending' | 'Approved' | 'Rejected' | 'Reassigned';

export interface MukandoVerificationRequestDto {
  id: number;
  mukandoRoundId: number;
  target: VerificationTarget;
  status: VerificationStatus;
  /** Zero (and name = 'Pending') until the verifier responds — prevents collusion. */
  assignedToUserId: number;
  assignedToName: string;
  expiresAt: string;
  createdAt: string;
  contributionId: number | null;
  contributorName: string | null;
  contributionAmount: number | null;
  rejectionReason: string | null;
}

export interface CycleMemberAgreementStatusDto {
  userId: number;
  userName: string;
  hasAgreed: boolean;
  agreedAt: string | null;
}

export interface CycleAgreementSummaryDto {
  allAgreed: boolean;
  agreedCount: number;
  totalCount: number;
  members: CycleMemberAgreementStatusDto[];
}
