using Microsoft.EntityFrameworkCore;
using Divvy.Api.Data;
using Divvy.Api.DTOs.Divvy;
using Divvy.Api.Models;

namespace Divvy.Api.Services;

public class ExpenseCycleService
{
    private readonly ApplicationDbContext _context;

    public ExpenseCycleService(ApplicationDbContext context)
    {
        _context = context;
    }

    // ── Queries ───────────────────────────────────────────────────────────────

    public async Task<List<ExpenseCycleSummaryDto>> GetAllAsync(int? groupId = null)
    {
        var query = _context.ExpenseCycles.AsQueryable();

        if (groupId.HasValue)
            query = query.Where(c => c.GroupId == groupId.Value);

        var cycles = await query
            .OrderByDescending(c => c.StartDate)
            .ToListAsync();

        return await BuildSummariesAsync(cycles);
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

        return await BuildSummariesAsync(cycles);
    }

    private async Task<List<ExpenseCycleSummaryDto>> BuildSummariesAsync(List<ExpenseCycle> cycles)
    {
        var groupIds   = cycles.Select(c => c.GroupId).Distinct().ToList();
        var groupNames = groupIds.Any()
            ? await _context.Groups
                .Where(g => groupIds.Contains(g.Id))
                .ToDictionaryAsync(g => g.Id, g => g.Name)
            : new Dictionary<int, string>();

        var summaries = new List<ExpenseCycleSummaryDto>();

        foreach (var cycle in cycles)
        {
            var memberCount = await _context.CycleMembers.CountAsync(m => m.ExpenseCycleId == cycle.Id);
            var expenses    = await _context.Expenses.Where(e => e.ExpenseCycleId == cycle.Id).ToListAsync();
            groupNames.TryGetValue(cycle.GroupId, out var groupName);

            summaries.Add(new ExpenseCycleSummaryDto(
                cycle.Id,
                cycle.Name,
                cycle.StartDate,
                cycle.EndDate,
                cycle.Status.ToString(),
                memberCount,
                expenses.Count,
                expenses.Sum(e => e.Amount),
                cycle.CreatedAt,
                cycle.GroupId,
                groupName ?? "(Unknown group)"));
        }

        return summaries;
    }

    public async Task<ExpenseCycleDto?> GetByIdAsync(int id)
    {
        var cycle = await _context.ExpenseCycles.FindAsync(id);
        if (cycle == null) return null;

        var members = await GetMemberDtosAsync(id);

        return new ExpenseCycleDto(
            cycle.Id,
            cycle.Name,
            cycle.StartDate,
            cycle.EndDate,
            cycle.Status.ToString(),
            cycle.CreatedByUserId,
            cycle.CreatedAt,
            members);
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
            // = sum of currentUser's obligations on memberId's expenses
            var owedByMe = obligations
                .Where(o => o.UserId == currentUserId)
                .Join(expenses.Where(e => e.PaidByUserId == memberId),
                      o => o.ExpenseId, e => e.Id, (o, _) => o.AmountOwed)
                .Sum();

            // Amount memberId owes currentUser:
            // = sum of memberId's obligations on currentUser's expenses
            var owedToMe = obligations
                .Where(o => o.UserId == memberId)
                .Join(expenses.Where(e => e.PaidByUserId == currentUserId),
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

    public async Task<(ExpenseCycleDto? dto, string? error)> CreateAsync(int createdByUserId, CreateExpenseCycleRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return (null, "Name is required.");

        if (request.GroupId <= 0)
            return (null, "A valid group is required.");

        var groupExists = await _context.Groups.AnyAsync(g => g.Id == request.GroupId && g.IsActive);
        if (!groupExists)
            return (null, "Group not found or is inactive.");

        if (request.StartDate >= request.EndDate)
            return (null, "EndDate must be after StartDate.");

        if (request.MemberUserIds == null || request.MemberUserIds.Count == 0)
            return (null, "At least one member is required.");

        var cycle = new ExpenseCycle
        {
            Name             = request.Name.Trim(),
            StartDate        = request.StartDate,
            EndDate          = request.EndDate,
            Status           = CycleStatus.Active,
            CreatedByUserId  = createdByUserId,
            GroupId          = request.GroupId,
            CreatedAt        = DateTime.UtcNow,
            UpdatedAt        = DateTime.UtcNow
        };

        _context.ExpenseCycles.Add(cycle);
        await _context.SaveChangesAsync();

        foreach (var uid in request.MemberUserIds.Distinct())
        {
            _context.CycleMembers.Add(new CycleMember
            {
                ExpenseCycleId = cycle.Id,
                UserId         = uid,
                AddedAt        = DateTime.UtcNow
            });
        }

        await _context.SaveChangesAsync();

        var members = await GetMemberDtosAsync(cycle.Id);

        return (new ExpenseCycleDto(
            cycle.Id, cycle.Name, cycle.StartDate, cycle.EndDate,
            cycle.Status.ToString(), cycle.CreatedByUserId, cycle.CreatedAt, members), null);
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

        var members = await GetMemberDtosAsync(cycle.Id);

        return (new ExpenseCycleDto(
            cycle.Id, cycle.Name, cycle.StartDate, cycle.EndDate,
            cycle.Status.ToString(), cycle.CreatedByUserId, cycle.CreatedAt, members), null);
    }

    public async Task<string?> CloseAsync(int id)
    {
        var cycle = await _context.ExpenseCycles.FindAsync(id);
        if (cycle == null) return "Cycle not found.";
        if (cycle.Status == CycleStatus.Closed) return "Cycle is already closed.";

        cycle.Status    = CycleStatus.Closed;
        cycle.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
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
        return null;
    }

    public async Task<string?> RemoveMemberAsync(int cycleId, int userId)
    {
        var member = await _context.CycleMembers
            .FirstOrDefaultAsync(m => m.ExpenseCycleId == cycleId && m.UserId == userId);
        if (member == null) return "User is not a member of this cycle.";

        _context.CycleMembers.Remove(member);
        await _context.SaveChangesAsync();
        return null;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<List<CycleMemberDto>> GetMemberDtosAsync(int cycleId)
    {
        var memberIds = await _context.CycleMembers
            .Where(m => m.ExpenseCycleId == cycleId)
            .Select(m => m.UserId)
            .ToListAsync();

        var users = await _context.Users
            .Where(u => memberIds.Contains(u.Id))
            .ToListAsync();

        return users.Select(u => new CycleMemberDto(u.Id, u.FirstName, u.LastName, u.Email)).ToList();
    }
}
