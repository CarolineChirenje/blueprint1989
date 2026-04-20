using Microsoft.EntityFrameworkCore;
using Batanai.Api.Data;
using Batanai.Api.DTOs.Batanai;
using Batanai.Api.Models;

namespace Batanai.Api.Services;

public class ExpenseService
{
    private readonly ApplicationDbContext _context;

    public ExpenseService(ApplicationDbContext context)
    {
        _context = context;
    }

    // ── Queries ───────────────────────────────────────────────────────────────

    public async Task<List<ExpenseDto>> GetByCycleAsync(int cycleId)
    {
        var expenses = await _context.Expenses
            .Where(e => e.ExpenseCycleId == cycleId)
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync();

        var result = new List<ExpenseDto>();
        foreach (var expense in expenses)
            result.Add(await BuildDtoAsync(expense));

        return result;
    }

    public async Task<ExpenseDto?> GetByIdAsync(int id)
    {
        var expense = await _context.Expenses.FindAsync(id);
        if (expense == null) return null;
        return await BuildDtoAsync(expense);
    }

    public async Task<List<MemberObligationDto>> GetObligationsForUserAsync(int userId)
    {
        var obligations = await _context.MemberObligations
            .Where(o => o.UserId == userId && !o.IsSettled)
            .OrderBy(o => o.CreatedAt)
            .ToListAsync();

        var result = new List<MemberObligationDto>();
        foreach (var o in obligations)
        {
            var user = await _context.Users.FindAsync(o.UserId);
            result.Add(new MemberObligationDto(
                o.Id, o.ExpenseId, o.UserId,
                user?.FirstName ?? "", user?.LastName ?? "",
                o.AmountOwed, o.IsSettled, o.SettledAt));
        }
        return result;
    }

    // ── Mutations ─────────────────────────────────────────────────────────────

    public async Task<(ExpenseDto? dto, string? error)> CreateAsync(int loggedByUserId, CreateExpenseRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            return (null, "Title is required.");

        if (request.Amount <= 0)
            return (null, "Amount must be greater than zero.");

        var cycle = await _context.ExpenseCycles.FindAsync(request.ExpenseCycleId);
        if (cycle == null) return (null, "Expense cycle not found.");
        if (cycle.Status == CycleStatus.Closed) return (null, "Cannot add expenses to a closed cycle.");

        if (!Enum.TryParse<ExpenseCategory>(request.Category, ignoreCase: true, out var category))
            return (null, $"Invalid category '{request.Category}'.");

        var expense = new Expense
        {
            ExpenseCycleId = request.ExpenseCycleId,
            Title          = request.Title.Trim(),
            Amount         = Math.Round(request.Amount, 2),
            Category       = category,
            LoggedByUserId = loggedByUserId,
            Notes          = request.Notes?.Trim(),
            CreatedAt      = DateTime.UtcNow,
            UpdatedAt      = DateTime.UtcNow
        };

        _context.Expenses.Add(expense);
        await _context.SaveChangesAsync();

        await RecalculateObligationsAsync(expense);

        return (await BuildDtoAsync(expense), null);
    }

    public async Task<(ExpenseDto? dto, string? error)> UpdateAsync(int id, int currentUserId, UpdateExpenseRequest request)
    {
        var expense = await _context.Expenses.FindAsync(id);
        if (expense == null) return (null, "Expense not found.");

        var cycle = await _context.ExpenseCycles.FindAsync(expense.ExpenseCycleId);
        if (cycle?.Status == CycleStatus.Closed) return (null, "Cannot modify expenses in a closed cycle.");

        if (string.IsNullOrWhiteSpace(request.Title))
            return (null, "Title is required.");

        if (request.Amount <= 0)
            return (null, "Amount must be greater than zero.");

        if (!Enum.TryParse<ExpenseCategory>(request.Category, ignoreCase: true, out var category))
            return (null, $"Invalid category '{request.Category}'.");

        expense.Title     = request.Title.Trim();
        expense.Amount    = Math.Round(request.Amount, 2);
        expense.Category  = category;
        expense.Notes     = request.Notes?.Trim();
        expense.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        // Remove old obligations and recompute
        var old = _context.MemberObligations.Where(o => o.ExpenseId == expense.Id);
        _context.MemberObligations.RemoveRange(old);
        await _context.SaveChangesAsync();

        await RecalculateObligationsAsync(expense);

        return (await BuildDtoAsync(expense), null);
    }

    public async Task<string?> DeleteAsync(int id)
    {
        var expense = await _context.Expenses.FindAsync(id);
        if (expense == null) return "Expense not found.";

        var cycle = await _context.ExpenseCycles.FindAsync(expense.ExpenseCycleId);
        if (cycle?.Status == CycleStatus.Closed) return "Cannot delete expenses in a closed cycle.";

        var obligations = _context.MemberObligations.Where(o => o.ExpenseId == id);
        _context.MemberObligations.RemoveRange(obligations);
        _context.Expenses.Remove(expense);

        await _context.SaveChangesAsync();
        return null;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Recalculates obligations for all expenses in a cycle based on the current member list.
    /// Called after member changes (opt-out approval or direct removal) in Majana cycles.
    /// </summary>
    public async Task RecalculateAllObligationsAsync(int cycleId)
    {
        var expenses = await _context.Expenses
            .Where(e => e.ExpenseCycleId == cycleId)
            .ToListAsync();

        foreach (var expense in expenses)
        {
            // Remove existing obligations for this expense
            var existing = await _context.MemberObligations
                .Where(o => o.ExpenseId == expense.Id)
                .ToListAsync();
            _context.MemberObligations.RemoveRange(existing);

            await RecalculateObligationsAsync(expense);
        }
    }

    private async Task RecalculateObligationsAsync(Expense expense)
    {
        // Only Participants owe shares; Observers are excluded from financial obligations
        var memberIds = await _context.CycleMembers
            .Where(m => m.ExpenseCycleId == expense.ExpenseCycleId && m.CycleRole == CycleRole.Participant)
            .Select(m => m.UserId)
            .ToListAsync();

        // All members owe an equal share (logger is audit trail only)
        var debtors = memberIds;
        if (debtors.Count == 0) return;

        // Equal split across all members
        int totalMembers = memberIds.Count;
        decimal share = Math.Round(expense.Amount / totalMembers, 2);

        foreach (var uid in debtors)
        {
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
    }

    private async Task<ExpenseDto> BuildDtoAsync(Expense expense)
    {
        var logger = await _context.Users.FindAsync(expense.LoggedByUserId);
        var loggerName = logger != null ? $"{logger.FirstName} {logger.LastName}".Trim() : "Unknown";

        var obligations = await _context.MemberObligations
            .Where(o => o.ExpenseId == expense.Id)
            .ToListAsync();

        var obDtos = new List<MemberObligationDto>();
        foreach (var o in obligations)
        {
            var user = await _context.Users.FindAsync(o.UserId);
            obDtos.Add(new MemberObligationDto(
                o.Id, o.ExpenseId, o.UserId,
                user?.FirstName ?? "", user?.LastName ?? "",
                o.AmountOwed, o.IsSettled, o.SettledAt));
        }

        return new ExpenseDto(
            expense.Id,
            expense.ExpenseCycleId,
            expense.Title,
            expense.Amount,
            expense.Category.ToString(),
            expense.LoggedByUserId,
            loggerName,
            expense.Notes,
            expense.CreatedAt,
            obDtos);
    }
}
