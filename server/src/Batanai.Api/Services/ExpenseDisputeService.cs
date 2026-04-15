using Microsoft.EntityFrameworkCore;
using Batanai.Api.Data;
using Batanai.Api.DTOs.Batanai;
using Batanai.Api.Models;

namespace Batanai.Api.Services;

public class ExpenseDisputeService
{
    private readonly ApplicationDbContext    _context;
    private readonly IPushNotificationSender _push;

    public ExpenseDisputeService(ApplicationDbContext context, IPushNotificationSender push)
    {
        _context = context;
        _push    = push;
    }

    public async Task<List<ExpenseDisputeDto>> GetByCycleAsync(int cycleId)
    {
        var expenseIds = await _context.Expenses
            .Where(e => e.ExpenseCycleId == cycleId)
            .Select(e => e.Id)
            .ToListAsync();

        var disputes = await _context.ExpenseDisputes
            .Where(d => expenseIds.Contains(d.ExpenseId))
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync();

        return await BuildDtosAsync(disputes);
    }

    public async Task<(ExpenseDisputeDto? dto, string? error)> CreateAsync(int raisedByUserId, CreateDisputeRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
            return (null, "Reason is required.");

        var expense = await _context.Expenses.FindAsync(request.ExpenseId);
        if (expense == null) return (null, "Expense not found.");

        var cycle = await _context.ExpenseCycles.FindAsync(expense.ExpenseCycleId);
        if (cycle == null) return (null, "Cycle not found.");
        if (cycle.Status != CycleStatus.Draft)
            return (null, "Disputes can only be raised while the cycle is in Draft status.");

        var isMember = await _context.CycleMembers
            .AnyAsync(m => m.ExpenseCycleId == cycle.Id && m.UserId == raisedByUserId);
        if (!isMember) return (null, "You are not a member of this cycle.");

        var dispute = new ExpenseDispute
        {
            ExpenseId       = request.ExpenseId,
            RaisedByUserId  = raisedByUserId,
            Reason          = request.Reason.Trim(),
            Status          = DisputeStatus.Pending,
            CreatedAt       = DateTime.UtcNow,
            UpdatedAt       = DateTime.UtcNow
        };

        _context.ExpenseDisputes.Add(dispute);
        await _context.SaveChangesAsync();

        // Notify all cycle members
        var raiser = await _context.Users.FindAsync(raisedByUserId);
        var raiserName = $"{raiser?.FirstName} {raiser?.LastName}".Trim();
        var memberIds = await _context.CycleMembers
            .Where(m => m.ExpenseCycleId == cycle.Id).Select(m => m.UserId).ToListAsync();

        await _push.SendToUsersAsync(
            memberIds,
            NotificationType.DisputeRaised,
            $"Expense disputed in {cycle.Name}",
            $"{raiserName} raised a dispute on \"{expense.Title}\": {dispute.Reason}",
            $"/cycles/{cycle.Id}",
            dispute.Id);

        return (await BuildDtoAsync(dispute), null);
    }

    public async Task<(ExpenseDisputeDto? dto, string? error)> UpdateStatusAsync(int disputeId, UpdateDisputeStatusRequest request)
    {
        var dispute = await _context.ExpenseDisputes.FindAsync(disputeId);
        if (dispute == null) return (null, "Dispute not found.");

        if (!Enum.TryParse<DisputeStatus>(request.Status, ignoreCase: true, out var newStatus))
            return (null, $"Invalid status '{request.Status}'. Valid values: Reviewed, Resolved, Rejected.");

        // Cannot move back to Pending via API
        if (newStatus == DisputeStatus.Pending)
            return (null, "Cannot set status back to Pending.");

        dispute.Status     = newStatus;
        dispute.AdminNotes = request.AdminNotes?.Trim();
        dispute.UpdatedAt  = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        // Notify the person who raised the dispute
        var expense = await _context.Expenses.FindAsync(dispute.ExpenseId);
        var cycle   = expense != null ? await _context.ExpenseCycles.FindAsync(expense.ExpenseCycleId) : null;
        var deepLink = cycle != null ? $"/cycles/{cycle.Id}" : null;

        await _push.SendToUserAsync(
            dispute.RaisedByUserId,
            NotificationType.DisputeUpdated,
            $"Your dispute has been {newStatus.ToString().ToLower()}",
            $"Your dispute on \"{expense?.Title}\" has been marked as {newStatus.ToString().ToLower()}." +
            (string.IsNullOrWhiteSpace(dispute.AdminNotes) ? "" : $" Admin note: {dispute.AdminNotes}"),
            deepLink,
            dispute.Id);

        return (await BuildDtoAsync(dispute), null);
    }

    public async Task<bool> CanUserManageDisputeAsync(int disputeId, int userId, ExpenseCycleService cycleService, IGroupService groupService)
    {
        var dispute = await _context.ExpenseDisputes.FindAsync(disputeId);
        if (dispute == null) return false;
        var expense = await _context.Expenses.FindAsync(dispute.ExpenseId);
        if (expense == null) return false;
        var groupId = await cycleService.GetCycleGroupIdAsync(expense.ExpenseCycleId);
        if (groupId == null) return false;
        return await groupService.IsGroupAdminOfGroupAsync(groupId.Value, userId);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<List<ExpenseDisputeDto>> BuildDtosAsync(List<ExpenseDispute> disputes)
    {
        var result = new List<ExpenseDisputeDto>();
        foreach (var d in disputes)
            result.Add(await BuildDtoAsync(d));
        return result;
    }

    private async Task<ExpenseDisputeDto> BuildDtoAsync(ExpenseDispute dispute)
    {
        var expense = await _context.Expenses.FindAsync(dispute.ExpenseId);
        var raiser  = await _context.Users.FindAsync(dispute.RaisedByUserId);
        return new ExpenseDisputeDto(
            dispute.Id,
            dispute.ExpenseId,
            expense?.Title ?? "(unknown)",
            expense?.ExpenseCycleId ?? 0,
            dispute.RaisedByUserId,
            $"{raiser?.FirstName} {raiser?.LastName}".Trim(),
            dispute.Reason,
            dispute.Status.ToString(),
            dispute.AdminNotes,
            dispute.CreatedAt,
            dispute.UpdatedAt);
    }
}
