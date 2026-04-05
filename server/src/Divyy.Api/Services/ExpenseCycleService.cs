using Microsoft.EntityFrameworkCore;
using Divvy.Api.Data;
using Divvy.Api.DTOs.Divvy;
using Divvy.Api.Models;

namespace Divvy.Api.Services;

public class ExpenseCycleService
{
    private readonly ApplicationDbContext    _context;
    private readonly IPushNotificationSender _push;

    public ExpenseCycleService(ApplicationDbContext context, IPushNotificationSender push)
    {
        _context = context;
        _push    = push;
    }

    // ── Queries ───────────────────────────────────────────────────────────────

    public async Task<List<ExpenseCycleSummaryDto>> GetAllAsync(int currentUserId, int? groupId = null)
    {
        var query = _context.ExpenseCycles.AsQueryable();

        if (groupId.HasValue)
            query = query.Where(c => c.GroupId == groupId.Value);

        var cycles = await query
            .OrderByDescending(c => c.StartDate)
            .ToListAsync();

        return await BuildSummariesAsync(cycles, currentUserId);
    }

    public async Task<List<ExpenseCycleSummaryDto>> GetForUserAsync(int userId, int? groupId = null)
    {
        var cycleIds = await _context.CycleMembers
            .Where(m => m.UserId == userId)
            .Select(m => m.ExpenseCycleId)
            .ToListAsync();

        var query = _context.ExpenseCycles.Where(c => cycleIds.Contains(c.Id));

        if (groupId.HasValue)
            query = query.Where(c => c.GroupId == groupId.Value);

        var cycles = await query
            .OrderByDescending(c => c.StartDate)
            .ToListAsync();

        return await BuildSummariesAsync(cycles, userId);
    }

    private async Task<List<ExpenseCycleSummaryDto>> BuildSummariesAsync(List<ExpenseCycle> cycles, int currentUserId)
    {
        var groupIds   = cycles.Select(c => c.GroupId).Distinct().ToList();
        var groupNames = groupIds.Any()
            ? await _context.Groups
                .Where(g => groupIds.Contains(g.Id))
                .ToDictionaryAsync(g => g.Id, g => g.Name)
            : new Dictionary<int, string>();

        // Look up the current user's group role for all relevant groups in one query
        var groupRoles = groupIds.Any()
            ? await _context.GroupMembers
                .Where(gm => gm.UserId == currentUserId && groupIds.Contains(gm.GroupId))
                .ToDictionaryAsync(gm => gm.GroupId, gm => gm.GroupRole.ToString())
            : new Dictionary<int, string>();

        // Currency lookup
        var currencyIds = cycles.Select(c => c.CurrencyId).Distinct().ToList();
        var currencies = currencyIds.Any()
            ? await _context.Currencies
                .Where(cur => currencyIds.Contains(cur.Id))
                .ToDictionaryAsync(cur => cur.Id, cur => cur)
            : new Dictionary<int, Currency>();

        var summaries = new List<ExpenseCycleSummaryDto>();

        foreach (var cycle in cycles)
        {
            var memberCount = await _context.CycleMembers.CountAsync(m => m.ExpenseCycleId == cycle.Id);
            var expenses    = await _context.Expenses.Where(e => e.ExpenseCycleId == cycle.Id).ToListAsync();
            groupNames.TryGetValue(cycle.GroupId, out var groupName);
            groupRoles.TryGetValue(cycle.GroupId, out var groupRole);
            currencies.TryGetValue(cycle.CurrencyId, out var currency);

            summaries.Add(new ExpenseCycleSummaryDto(
                cycle.Id,
                cycle.Name,
                cycle.StartDate,
                cycle.EndDate,
                cycle.Status.ToString(),
                cycle.CycleType.ToString(),
                memberCount,
                expenses.Count,
                expenses.Sum(e => e.Amount),
                cycle.CreatedAt,
                cycle.GroupId,
                groupName ?? "(Unknown group)",
                groupRole ?? "GroupMember",
                currency?.Code ?? "USD",
                currency?.Symbol ?? "$"));
        }

        return summaries;
    }

    public async Task<ExpenseCycleDto?> GetByIdAsync(int id, int currentUserId = 0)
    {
        var cycle = await _context.ExpenseCycles.FindAsync(id);
        if (cycle == null) return null;

        var members = await GetMemberDtosAsync(id, cycle.GroupId);
        var role = currentUserId > 0
            ? await GetUserGroupRoleAsync(cycle.GroupId, currentUserId)
            : "GroupMember";
        var currency = await _context.Currencies.FindAsync(cycle.CurrencyId);

        return new ExpenseCycleDto(
            cycle.Id,
            cycle.Name,
            cycle.StartDate,
            cycle.EndDate,
            cycle.Status.ToString(),
            cycle.SplitType.ToString(),
            cycle.CycleType.ToString(),
            cycle.CreatedByUserId,
            cycle.CreatedAt,
            members,
            role,
            cycle.GroupId,
            cycle.CurrencyId,
            currency?.Code ?? "USD",
            currency?.Symbol ?? "$",
            cycle.ContributionAmount,
            cycle.Frequency?.ToString());
    }

    public async Task<CycleBalanceDto?> GetBalanceAsync(int cycleId, int currentUserId)
    {
        var cycle = await _context.ExpenseCycles.FindAsync(cycleId);
        if (cycle == null) return null;

        var memberIds = await _context.CycleMembers
            .Where(m => m.ExpenseCycleId == cycleId)
            .Select(m => m.UserId)
            .ToListAsync();

        var expenses = await _context.Expenses
            .Where(e => e.ExpenseCycleId == cycleId)
            .ToListAsync();

        var obligations = await _context.MemberObligations
            .Where(o => expenses.Select(e => e.Id).Contains(o.ExpenseId))
            .ToListAsync();

        var confirmedPayments = await _context.Payments
            .Where(p => p.ExpenseCycleId == cycleId && p.Status == PaymentStatus.Confirmed)
            .ToListAsync();

        var users = await _context.Users
            .Where(u => memberIds.Contains(u.Id))
            .ToListAsync();

        var balances = new List<MemberBalanceDto>();

        foreach (var memberId in memberIds.Where(m => m != currentUserId))
        {
            // Amount currentUser owes memberId:
            // = sum of currentUser's obligations on memberId's logged expenses
            var owedByMe = obligations
                .Where(o => o.UserId == currentUserId)
                .Join(expenses.Where(e => e.LoggedByUserId == memberId),
                      o => o.ExpenseId, e => e.Id, (o, _) => o.AmountOwed)
                .Sum();

            // Amount memberId owes currentUser:
            // = sum of memberId's obligations on currentUser's logged expenses
            var owedToMe = obligations
                .Where(o => o.UserId == memberId)
                .Join(expenses.Where(e => e.LoggedByUserId == currentUserId),
                      o => o.ExpenseId, e => e.Id, (o, _) => o.AmountOwed)
                .Sum();

            // Confirmed payments:
            var paidByMe  = confirmedPayments.Where(p => p.PayerId == currentUserId && p.PayeeId == memberId).Sum(p => p.Amount);
            var paidByThem = confirmedPayments.Where(p => p.PayerId == memberId && p.PayeeId == currentUserId).Sum(p => p.Amount);

            // Net: positive = they owe me, negative = I owe them
            decimal net = (owedToMe - paidByThem) - (owedByMe - paidByMe);

            var user = users.FirstOrDefault(u => u.Id == memberId);
            balances.Add(new MemberBalanceDto(
                memberId,
                user?.FirstName ?? "Unknown",
                user?.LastName ?? "",
                Math.Round(net, 2)));
        }

        return new CycleBalanceDto(cycleId, cycle.Name, balances);
    }

    // ── Mutations ─────────────────────────────────────────────────────────────

    /// <summary>Returns the GroupId of a cycle, or null if the cycle does not exist.</summary>
    public async Task<int?> GetCycleGroupIdAsync(int cycleId)
        => await _context.ExpenseCycles
            .Where(c => c.Id == cycleId)
            .Select(c => (int?)c.GroupId)
            .FirstOrDefaultAsync();

    public async Task<CycleType?> GetCycleTypeAsync(int cycleId)
        => await _context.ExpenseCycles
            .Where(c => c.Id == cycleId)
            .Select(c => (CycleType?)c.CycleType)
            .FirstOrDefaultAsync();

    /// <summary>Duplicate a completed cycle as a new Draft (for "Create Another").</summary>
    public async Task<(ExpenseCycleDto? dto, string? error)> DuplicateAsync(int sourceCycleId, int createdByUserId, DateTime newStartDate)
    {
        var source = await _context.ExpenseCycles.FindAsync(sourceCycleId);
        if (source == null) return (null, "Source cycle not found.");

        var memberIds = await _context.CycleMembers
            .Where(m => m.ExpenseCycleId == sourceCycleId)
            .Select(m => m.UserId)
            .ToListAsync();

        // For Mukando, randomize payout order
        List<int>? payoutOrder = null;
        if (source.CycleType == CycleType.Mukando)
        {
            payoutOrder = memberIds.OrderBy(_ => Random.Shared.Next()).ToList();
        }

        var request = new CreateExpenseCycleRequest(
            Name: $"{source.Name} (copy)",
            StartDate: newStartDate,
            EndDate: newStartDate.AddMonths(1), // placeholder, gets recalculated for Mukando
            MemberUserIds: memberIds,
            GroupId: source.GroupId,
            CurrencyId: source.CurrencyId,
            CycleType: source.CycleType.ToString(),
            ContributionAmount: source.ContributionAmount,
            Frequency: source.Frequency?.ToString(),
            PayoutOrder: payoutOrder);

        return await CreateAsync(createdByUserId, request);
    }

    public async Task<(ExpenseCycleDto? dto, string? error)> CreateAsync(int createdByUserId, CreateExpenseCycleRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return (null, "Name is required.");

        if (request.GroupId <= 0)
            return (null, "A valid group is required.");

        var groupExists = await _context.Groups.AnyAsync(g => g.Id == request.GroupId && g.IsActive);
        if (!groupExists)
            return (null, "Group not found or is inactive.");

        // Parse cycle type early so we can skip EndDate check for Mukando (auto-calculated)
        if (!Enum.TryParse<CycleType>(request.CycleType, true, out var cycleType))
            return (null, "Invalid cycle type. Must be 'Majana' or 'Mukando'.");

        if (cycleType != CycleType.Mukando && request.StartDate >= request.EndDate)
            return (null, "EndDate must be after StartDate.");

        var currencyExists = await _context.Currencies.AnyAsync(c => c.Id == request.CurrencyId && c.IsActive);
        if (!currencyExists)
            return (null, "Invalid currency.");

        // Mukando-specific validation
        CycleFrequency? frequency = null;
        if (cycleType == CycleType.Mukando)
        {
            if (request.ContributionAmount is null or <= 0)
                return (null, "Contribution amount is required and must be positive for Mukando cycles.");
            if (string.IsNullOrWhiteSpace(request.Frequency) || !Enum.TryParse<CycleFrequency>(request.Frequency, true, out var freq))
                return (null, "Frequency is required for Mukando cycles (Weekly, Biweekly, Monthly).");
            frequency = freq;
            if (request.MemberUserIds == null || request.MemberUserIds.Count < 2)
                return (null, "Mukando cycles require at least 2 members.");
            if (request.PayoutOrder == null || request.PayoutOrder.Count == 0)
                return (null, "Payout order is required for Mukando cycles.");
            var memberSet = new HashSet<int>(request.MemberUserIds) { createdByUserId };
            var payoutSet = new HashSet<int>(request.PayoutOrder);
            if (!payoutSet.SetEquals(memberSet))
                return (null, "Payout order must contain exactly the same members as the member list.");
            if (request.PayoutOrder.Count != request.PayoutOrder.Distinct().Count())
                return (null, "Payout order must not contain duplicate members.");
        }

        var cycle = new ExpenseCycle
        {
            Name               = request.Name.Trim(),
            StartDate          = request.StartDate,
            EndDate            = request.EndDate,
            Status             = CycleStatus.Draft,
            CycleType          = cycleType,
            ContributionAmount = cycleType == CycleType.Mukando ? request.ContributionAmount : null,
            Frequency          = frequency,
            CurrencyId         = request.CurrencyId,
            CreatedByUserId    = createdByUserId,
            GroupId            = request.GroupId,
            CreatedAt          = DateTime.UtcNow,
            UpdatedAt          = DateTime.UtcNow
        };

        _context.ExpenseCycles.Add(cycle);
        await _context.SaveChangesAsync();

        // Auto-add creator as a member
        var memberIds = new HashSet<int> { createdByUserId };
        if (request.MemberUserIds != null)
            foreach (var uid in request.MemberUserIds) memberIds.Add(uid);

        foreach (var uid in memberIds)
        {
            _context.CycleMembers.Add(new CycleMember
            {
                ExpenseCycleId = cycle.Id,
                UserId         = uid,
                AddedAt        = DateTime.UtcNow
            });
        }

        await _context.SaveChangesAsync();

        // Notify added members (excluding the creator) that they have been added to the cycle
        var notifyIds = memberIds.Where(id => id != createdByUserId).ToList();
        if (notifyIds.Count > 0)
        {
            var group = await _context.Groups.FindAsync(cycle.GroupId);
            var groupName = group?.Name ?? "your group";
            var typeLabel = cycleType == CycleType.Mukando ? "Mukando" : "";
            await _push.SendToUsersAsync(
                notifyIds,
                NotificationType.CycleMemberAdded,
                $"Added to {cycle.Name}",
                $"You have been added to the {typeLabel} cycle \"{cycle.Name}\" in {groupName}.".Trim(),
                $"/cycles/{cycle.Id}",
                cycle.Id);
        }

        // Mukando: generate rounds and contributions
        if (cycleType == CycleType.Mukando && request.PayoutOrder != null)
        {
            int memberCount = memberIds.Count;
            decimal contributionAmount = request.ContributionAmount!.Value;
            decimal expectedPool = contributionAmount * (memberCount - 1);

            for (int i = 0; i < request.PayoutOrder.Count; i++)
            {
                var recipientId = request.PayoutOrder[i];
                var dueDate = CalculateRoundDueDate(cycle.StartDate, frequency!.Value, i);

                var round = new MukandoRound
                {
                    ExpenseCycleId  = cycle.Id,
                    RoundNumber     = i + 1,
                    RecipientUserId = recipientId,
                    Status          = RoundStatus.Pending,
                    ExpectedPool    = expectedPool,
                    ActualCollected = 0,
                    PayoutConfirmed = false,
                    DueDate         = dueDate,
                    CreatedAt       = DateTime.UtcNow,
                    UpdatedAt       = DateTime.UtcNow
                };

                _context.MukandoRounds.Add(round);
                await _context.SaveChangesAsync();

                // Create contribution records for all members except the recipient
                foreach (var uid in memberIds.Where(m => m != recipientId))
                {
                    _context.MukandoContributions.Add(new MukandoContribution
                    {
                        MukandoRoundId = round.Id,
                        UserId         = uid,
                        Amount         = contributionAmount,
                        Status         = ContributionStatus.Pending,
                        CreatedAt      = DateTime.UtcNow
                    });
                }
            }

            // Update EndDate to match last round's due date
            cycle.EndDate = CalculateRoundDueDate(cycle.StartDate, frequency!.Value, request.PayoutOrder.Count - 1);
            await _context.SaveChangesAsync();
        }

        // Majana: copy expenses from another cycle if requested
        if (cycleType == CycleType.Majana && request.CopyExpensesFromCycleId.HasValue)
        {
            var sourceCycleExists = await _context.ExpenseCycles
                .AnyAsync(c => c.Id == request.CopyExpensesFromCycleId.Value && c.GroupId == request.GroupId);
            if (!sourceCycleExists)
                return (null, "Source cycle not found in this group.");

            var sourceExpenses = await _context.Expenses
                .Where(e => e.ExpenseCycleId == request.CopyExpensesFromCycleId.Value)
                .ToListAsync();

            foreach (var src in sourceExpenses)
            {
                _context.Expenses.Add(new Expense
                {
                    ExpenseCycleId  = cycle.Id,
                    Title           = src.Title,
                    Amount          = src.Amount,
                    Category        = src.Category,
                    Notes           = src.Notes,
                    LoggedByUserId  = createdByUserId,
                    CreatedAt       = DateTime.UtcNow,
                    UpdatedAt       = DateTime.UtcNow
                });
            }

            await _context.SaveChangesAsync();
        }

        return (await GetByIdAsync(cycle.Id, createdByUserId), null);
    }

    public static DateTime CalculateRoundDueDate(DateTime startDate, CycleFrequency frequency, int roundIndex)
    {
        return frequency switch
        {
            CycleFrequency.Weekly   => startDate.AddDays(7 * (roundIndex + 1)),
            CycleFrequency.Biweekly => startDate.AddDays(14 * (roundIndex + 1)),
            CycleFrequency.Monthly  => startDate.AddMonths(roundIndex + 1),
            _                       => startDate.AddMonths(roundIndex + 1)
        };
    }

    public async Task<(ExpenseCycleDto? dto, string? error)> StartAsync(int cycleId)
    {
        var cycle = await _context.ExpenseCycles.FindAsync(cycleId);
        if (cycle == null) return (null, "Cycle not found.");
        if (cycle.Status != CycleStatus.Draft) return (null, "Only a Draft cycle can be started.");

        var memberCount = await _context.CycleMembers.CountAsync(m => m.ExpenseCycleId == cycleId);
        if (memberCount < 2) return (null, "A cycle requires at least 2 members before it can be started.");

        // Mukando: cancel all pending swap requests
        if (cycle.CycleType == CycleType.Mukando)
        {
            var pendingSwaps = await _context.MukandoSwapRequests
                .Where(s => s.ExpenseCycleId == cycleId && s.Status == SwapRequestStatus.Pending)
                .ToListAsync();
            foreach (var swap in pendingSwaps)
            {
                swap.Status = SwapRequestStatus.Cancelled;
                swap.RespondedAt = DateTime.UtcNow;
            }
        }

        if (cycle.CycleType == CycleType.Majana)
        {
            var hasOpenDisputes = await _context.ExpenseDisputes
                .Join(_context.Expenses.Where(e => e.ExpenseCycleId == cycleId),
                      d => d.ExpenseId, e => e.Id, (d, _) => d)
                .AnyAsync(d => d.Status == DisputeStatus.Pending || d.Status == DisputeStatus.Reviewed);
            if (hasOpenDisputes) return (null, "All expense disputes must be resolved or rejected before starting the cycle.");
        }

        cycle.Status    = CycleStatus.Active;
        cycle.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        var memberIds = await _context.CycleMembers
            .Where(m => m.ExpenseCycleId == cycleId).Select(m => m.UserId).ToListAsync();

        if (cycle.CycleType == CycleType.Mukando)
        {
            // Activate the first round
            var firstRound = await _context.MukandoRounds
                .Where(r => r.ExpenseCycleId == cycleId && r.RoundNumber == 1)
                .FirstOrDefaultAsync();
            if (firstRound != null)
            {
                firstRound.Status = RoundStatus.Active;
                firstRound.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                var recipient = await _context.Users.FindAsync(firstRound.RecipientUserId);
                var currency = await _context.Currencies.FindAsync(cycle.CurrencyId);
                var symbol = currency?.Symbol ?? "$";
                var recipientName = recipient != null ? $"{recipient.FirstName} {recipient.LastName}" : "Unknown";

                await _push.SendToUsersAsync(
                    memberIds,
                    NotificationType.MukandoRoundStarted,
                    $"Mukando started: {cycle.Name}",
                    $"Round 1 has started. {recipientName} receives this round. Contribute {symbol}{cycle.ContributionAmount:F2} by {firstRound.DueDate:MMM d, yyyy}.",
                    $"/cycles/{cycleId}",
                    cycleId);
            }
        }
        else
        {
            // Majana: re-calculate obligations for any expenses added during Draft
            var expenses = await _context.Expenses.Where(e => e.ExpenseCycleId == cycleId).ToListAsync();
            foreach (var expense in expenses)
            {
                var old = _context.MemberObligations.Where(o => o.ExpenseId == expense.Id);
                _context.MemberObligations.RemoveRange(old);
            }
            await _context.SaveChangesAsync();

            foreach (var expense in expenses)
            {
                if (memberIds.Count == 0) continue;
                decimal share = Math.Round(expense.Amount / memberIds.Count, 2);
                foreach (var uid in memberIds)
                    _context.MemberObligations.Add(new MemberObligation
                    {
                        ExpenseId  = expense.Id,
                        UserId     = uid,
                        AmountOwed = share,
                        IsSettled  = false,
                        CreatedAt  = DateTime.UtcNow
                    });
            }
            await _context.SaveChangesAsync();

            // Notify all members that the cycle has started
            var totalExpenses = expenses.Sum(e => e.Amount);
            decimal sharePerMember = memberCount > 0 ? Math.Round(totalExpenses / memberCount, 2) : 0;
            var currency = await _context.Currencies.FindAsync(cycle.CurrencyId);
            var sym = currency?.Symbol ?? "$";
            await _push.SendToUsersAsync(
                memberIds,
                NotificationType.CycleStarted,
                $"Cycle started: {cycle.Name}",
                $"The cycle has started. Your share is {sym}{sharePerMember:F2}. Due by {cycle.EndDate:MMM d, yyyy}.",
                $"/cycles/{cycleId}",
                cycleId);
        }

        cycle.StartNotificationSent = true;
        await _context.SaveChangesAsync();

        return (await GetByIdAsync(cycle.Id, 0), null);
    }

    public async Task<(ExpenseCycleDto? dto, string? error)> UpdateAsync(int id, UpdateExpenseCycleRequest request)
    {
        var cycle = await _context.ExpenseCycles.FindAsync(id);
        if (cycle == null) return (null, "Cycle not found.");

        if (string.IsNullOrWhiteSpace(request.Name))
            return (null, "Name is required.");

        if (request.StartDate >= request.EndDate)
            return (null, "EndDate must be after StartDate.");

        cycle.Name      = request.Name.Trim();
        cycle.StartDate = request.StartDate;
        cycle.EndDate   = request.EndDate;
        cycle.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return (await GetByIdAsync(cycle.Id, 0), null);
    }

    public async Task<string?> CloseAsync(int id)
    {
        var cycle = await _context.ExpenseCycles.FindAsync(id);
        if (cycle == null) return "Cycle not found.";
        if (cycle.Status == CycleStatus.Draft) return "Cannot close a Draft cycle — start it first.";
        if (cycle.Status == CycleStatus.Closed) return "Cycle is already closed.";

        cycle.Status    = CycleStatus.Closed;
        cycle.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        var memberIds = await _context.CycleMembers
            .Where(m => m.ExpenseCycleId == id).Select(m => m.UserId).ToListAsync();
        if (memberIds.Count > 0)
            await _push.SendToUsersAsync(
                memberIds,
                NotificationType.CycleClosed,
                $"Cycle closed: {cycle.Name}",
                "The cycle has been closed by the admin. Check the summary for final balances.",
                $"/cycles/{id}",
                id);

        return null;
    }

    public async Task<string?> DeleteAsync(int id)
    {
        var cycle = await _context.ExpenseCycles.FindAsync(id);
        if (cycle == null) return "Cycle not found.";

        // Cascade: expenses and obligations are deleted via DB cascade
        _context.ExpenseCycles.Remove(cycle);
        await _context.SaveChangesAsync();
        return null;
    }

    public async Task<string?> AddMemberAsync(int cycleId, int userId)
    {
        var cycle = await _context.ExpenseCycles.FindAsync(cycleId);
        if (cycle == null) return "Cycle not found.";
        if (cycle.Status != CycleStatus.Draft) return "Cannot add members to an active or closed cycle.";

        var exists = await _context.CycleMembers
            .AnyAsync(m => m.ExpenseCycleId == cycleId && m.UserId == userId);
        if (exists) return "User is already a member of this cycle.";

        _context.CycleMembers.Add(new CycleMember
        {
            ExpenseCycleId = cycleId,
            UserId         = userId,
            AddedAt        = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();

        var group = await _context.Groups.FindAsync(cycle.GroupId);
        var groupName = group?.Name ?? "your group";
        await _push.SendToUserAsync(
            userId,
            NotificationType.CycleMemberAdded,
            $"Added to {cycle.Name}",
            $"You have been added to the {cycle.Name} cycle in {groupName}.",
            $"/cycles/{cycleId}",
            cycleId);

        return null;
    }

    public async Task<string?> RemoveMemberAsync(int cycleId, int userId)
    {
        var member = await _context.CycleMembers
            .FirstOrDefaultAsync(m => m.ExpenseCycleId == cycleId && m.UserId == userId);
        if (member == null) return "User is not a member of this cycle.";

        _context.CycleMembers.Remove(member);
        await _context.SaveChangesAsync();

        var cycle = await _context.ExpenseCycles.FindAsync(cycleId);
        if (cycle != null)
        {
            var group = await _context.Groups.FindAsync(cycle.GroupId);
            var groupName = group?.Name ?? "your group";
            await _push.SendToUserAsync(
                userId,
                NotificationType.CycleMemberRemoved,
                $"Removed from {cycle.Name}",
                $"You have been removed from the {cycle.Name} cycle in {groupName}.",
                $"/cycles/{cycleId}",
                cycleId);
        }

        return null;
    }

    // ── Contribution summary ──────────────────────────────────────────────────

    public async Task<CycleContributionSummaryDto?> GetContributionSummaryAsync(int cycleId)
    {
        var cycle = await _context.ExpenseCycles.FindAsync(cycleId);
        if (cycle == null) return null;

        var memberIds = await _context.CycleMembers
            .Where(m => m.ExpenseCycleId == cycleId).Select(m => m.UserId).ToListAsync();

        var users = await _context.Users.Where(u => memberIds.Contains(u.Id)).ToListAsync();

        var expenses = await _context.Expenses.Where(e => e.ExpenseCycleId == cycleId).ToListAsync();
        var totalExpenses = expenses.Sum(e => e.Amount);

        var confirmedPayments = await _context.Payments
            .Where(p => p.ExpenseCycleId == cycleId && p.Status == PaymentStatus.Confirmed)
            .ToListAsync();

        int memberCount = memberIds.Count;
        decimal sharePerMember = memberCount > 0 ? Math.Round(totalExpenses / memberCount, 2) : 0;

        var members = memberIds.Select(uid =>
        {
            var user      = users.FirstOrDefault(u => u.Id == uid);
            var totalPaid = confirmedPayments.Where(p => p.PayerId == uid).Sum(p => p.Amount);
            var balance   = Math.Round(totalPaid - sharePerMember, 2);
            return new CycleMemberContributionDto(
                uid,
                user?.FirstName ?? "Unknown",
                user?.LastName  ?? "",
                sharePerMember,
                totalPaid,
                balance,
                balance >= 0);
        }).ToList();

        return new CycleContributionSummaryDto(
            cycleId, cycle.Name, totalExpenses, memberCount, sharePerMember, members,
            Math.Round(members.Where(m => !m.IsSettled).Sum(m => Math.Abs(m.Balance)), 2));
    }

    public async Task<string?> SendReminderAsync(int cycleId)
    {
        var cycle = await _context.ExpenseCycles.FindAsync(cycleId);
        if (cycle == null) return "Cycle not found.";
        if (cycle.Status != CycleStatus.Active) return "Reminders can only be sent for active cycles.";

        var summary = await GetContributionSummaryAsync(cycleId);
        if (summary == null) return "Could not load contribution data.";

        var unsettled = summary.Members.Where(m => !m.IsSettled).ToList();
        if (!unsettled.Any()) return null; // everyone already settled

        foreach (var member in unsettled)
        {
            var owed      = member.ShareOwed.ToString("F2");
            var paid      = member.TotalPaid.ToString("F2");
            var remaining = Math.Abs(member.Balance).ToString("F2");
            await _push.SendToUsersAsync(
                new[] { member.UserId },
                NotificationType.ManualReminder,
                $"Payment reminder: {cycle.Name}",
                $"You owe ${owed} | Paid ${paid} | Remaining ${remaining}. Please settle up!",
                deepLinkUrl: $"/cycles/{cycleId}",
                relatedEntityId: cycleId);
        }

        return null;
    }

    // ── Outstanding summary (cross-cycle) ────────────────────────────────────

    public async Task<OutstandingSummaryDto> GetOutstandingSummaryAsync(int userId)
    {
        var cycleIds = await _context.CycleMembers
            .Where(m => m.UserId == userId)
            .Select(m => m.ExpenseCycleId)
            .ToListAsync();

        var activeCycles = await _context.ExpenseCycles
            .Where(c => cycleIds.Contains(c.Id) && c.Status == CycleStatus.Active)
            .ToListAsync();

        var items = new List<CycleOutstandingItemDto>();

        foreach (var cycle in activeCycles)
        {
            var memberCount   = await _context.CycleMembers.CountAsync(m => m.ExpenseCycleId == cycle.Id);
            var totalExpenses = await _context.Expenses
                .Where(e => e.ExpenseCycleId == cycle.Id)
                .SumAsync(e => e.Amount);

            decimal sharePerMember = memberCount > 0 ? Math.Round(totalExpenses / memberCount, 2) : 0;

            var totalPaid = await _context.Payments
                .Where(p => p.ExpenseCycleId == cycle.Id
                         && p.PayerId == userId
                         && p.Status == PaymentStatus.Confirmed)
                .SumAsync(p => p.Amount);

            var outstanding = Math.Max(0, Math.Round(sharePerMember - totalPaid, 2));

            items.Add(new CycleOutstandingItemDto(cycle.Id, cycle.Name, outstanding, sharePerMember, totalPaid));
        }

        return new OutstandingSummaryDto(
            Math.Round(items.Sum(c => c.Outstanding), 2),
            items.Count,
            items);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<string> GetUserGroupRoleAsync(int groupId, int userId)
    {
        var member = await _context.GroupMembers
            .FirstOrDefaultAsync(m => m.GroupId == groupId
                                   && m.UserId == userId
                                   && m.Status == GroupInviteStatus.Accepted);
        return member?.GroupRole == GroupRole.GroupAdmin ? "GroupAdmin" : "GroupMember";
    }

    private async Task<List<CycleMemberDto>> GetMemberDtosAsync(int cycleId, int groupId)
    {
        var memberIds = await _context.CycleMembers
            .Where(m => m.ExpenseCycleId == cycleId)
            .Select(m => m.UserId)
            .ToListAsync();

        var users = await _context.Users
            .Where(u => memberIds.Contains(u.Id))
            .ToListAsync();

        var groupRoles = await _context.GroupMembers
            .Where(gm => gm.GroupId == groupId && memberIds.Contains(gm.UserId))
            .ToDictionaryAsync(gm => gm.UserId, gm => gm.GroupRole.ToString());

        return users.Select(u => new CycleMemberDto(
            u.Id, u.FirstName, u.LastName, u.Email,
            groupRoles.TryGetValue(u.Id, out var gr) ? gr : "GroupMember")).ToList();
    }
}
