using Microsoft.EntityFrameworkCore;
using Batanai.Api.Data;
using Batanai.Api.DTOs.Batanai;
using Batanai.Api.Models;

namespace Batanai.Api.Services;

public class PaymentService
{
    private readonly ApplicationDbContext    _context;
    private readonly NotificationService     _notifications;
    private readonly IPushNotificationSender _push;

    public PaymentService(ApplicationDbContext context, NotificationService notifications, IPushNotificationSender push)
    {
        _context       = context;
        _notifications = notifications;
        _push          = push;
    }

    // ── Queries ───────────────────────────────────────────────────────────────

    public async Task<List<PaymentDto>> GetForCycleAsync(int cycleId)
    {
        var payments = await _context.Payments
            .Where(p => p.ExpenseCycleId == cycleId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

        return await BuildDtosAsync(payments);
    }

    public async Task<List<PaymentDto>> GetForUserAsync(int userId)
    {
        var payments = await _context.Payments
            .Where(p => p.PayerId == userId || p.PayeeId == userId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

        return await BuildDtosAsync(payments);
    }

    public async Task<PaymentDto?> GetByIdAsync(int id)
    {
        var payment = await _context.Payments.FindAsync(id);
        if (payment == null) return null;
        return (await BuildDtosAsync(new List<Payment> { payment })).FirstOrDefault();
    }

    // ── Mutations ─────────────────────────────────────────────────────────────

    public async Task<(PaymentDto? dto, string? error)> CreateAsync(int payerId, CreatePaymentRequest request)
    {
        if (request.Amount <= 0)
            return (null, "Amount must be greater than zero.");

        // Self-payment (payer == payee) is allowed — auto-confirmed with broadcast push
        bool isSelfPayment = request.PayeeId == payerId;

        var cycle = await _context.ExpenseCycles.FindAsync(request.ExpenseCycleId);
        if (cycle == null) return (null, "Expense cycle not found.");

        var payerIsMember = await _context.CycleMembers
            .AnyAsync(m => m.ExpenseCycleId == request.ExpenseCycleId && m.UserId == payerId);
        if (!payerIsMember) return (null, "You are not a member of this cycle.");

        if (!isSelfPayment)
        {
            var payeeIsMember = await _context.CycleMembers
                .AnyAsync(m => m.ExpenseCycleId == request.ExpenseCycleId && m.UserId == request.PayeeId);
            if (!payeeIsMember) return (null, "Payee is not a member of this cycle.");
        }

        var payment = new Payment
        {
            PayerId        = payerId,
            PayeeId        = request.PayeeId,
            ExpenseCycleId = request.ExpenseCycleId,
            Amount         = Math.Round(request.Amount, 2),
            Status         = isSelfPayment ? PaymentStatus.Confirmed : PaymentStatus.Pending,
            ConfirmedAt    = isSelfPayment ? DateTime.UtcNow : null,
            Notes          = request.Notes?.Trim(),
            CreatedAt      = DateTime.UtcNow
        };

        _context.Payments.Add(payment);
        await _context.SaveChangesAsync();

        var payer = await _context.Users.FindAsync(payerId);

        if (isSelfPayment)
        {
            // Auto-confirmed: broadcast push to all cycle members
            var memberIds = await _context.CycleMembers
                .Where(m => m.ExpenseCycleId == request.ExpenseCycleId)
                .Select(m => m.UserId)
                .ToListAsync();

            if (memberIds.Count > 0)
                await _push.SendToUsersAsync(
                    memberIds,
                    NotificationType.CyclePaymentMade,
                    $"Payment made in {cycle.Name}",
                    $"{payer?.FirstName} {payer?.LastName} paid ${payment.Amount:F2} toward \"{cycle.Name}\".",
                    $"/cycles/{request.ExpenseCycleId}",
                    payment.Id);
        }
        else
        {
            // Notify all accepted group admins for approval
            var adminIds = await _context.GroupMembers
                .Where(m => m.GroupId == cycle.GroupId
                         && m.GroupRole == GroupRole.GroupAdmin
                         && m.Status == GroupInviteStatus.Accepted)
                .Select(m => m.UserId)
                .ToListAsync();

            foreach (var adminId in adminIds)
            {
                await _notifications.CreateAsync(
                    adminId,
                    $"{payer?.FirstName} {payer?.LastName} sent a payment of ${payment.Amount:F2} — please review and confirm.",
                    NotificationType.PaymentReceived,
                    $"/cycles/{request.ExpenseCycleId}/payments",
                    payment.Id);
            }
        }

        var dto = (await BuildDtosAsync(new List<Payment> { payment })).First();
        return (dto, null);
    }

    public async Task<(PaymentDto? dto, string? error)> RespondAsync(int paymentId, int respondingUserId, bool confirm, string? notes)
    {
        var payment = await _context.Payments.FindAsync(paymentId);
        if (payment == null) return (null, "Payment not found.");

        if (payment.Status != PaymentStatus.Pending)
            return (null, "Payment has already been responded to.");

        // Only an accepted group admin of the cycle's group may confirm or reject
        var cycle = await _context.ExpenseCycles.FindAsync(payment.ExpenseCycleId);
        if (cycle == null) return (null, "Cycle not found.");

        var isGroupAdmin = await _context.GroupMembers
            .AnyAsync(m => m.GroupId == cycle.GroupId
                        && m.UserId == respondingUserId
                        && m.GroupRole == GroupRole.GroupAdmin
                        && m.Status == GroupInviteStatus.Accepted);

        if (!isGroupAdmin)
            return (null, "Only a group admin can confirm or reject a payment.");

        payment.Status      = confirm ? PaymentStatus.Confirmed : PaymentStatus.Rejected;
        payment.ConfirmedAt = confirm ? DateTime.UtcNow : null;
        if (!string.IsNullOrWhiteSpace(notes)) payment.Notes = notes.Trim();

        await _context.SaveChangesAsync();

        // Notify payer of the result
        var responder = await _context.Users.FindAsync(respondingUserId);
        var message = confirm
            ? $"{responder?.FirstName} {responder?.LastName} confirmed your payment of ${payment.Amount:F2}."
            : $"{responder?.FirstName} {responder?.LastName} rejected your payment of ${payment.Amount:F2}.";

        await _notifications.CreateAsync(
            payment.PayerId,
            message,
            NotificationType.PaymentReceived,
            $"/cycles/{payment.ExpenseCycleId}/payments",
            payment.Id);

        // On confirmation: push to ALL cycle members that a payment was made
        if (confirm)
        {
            var payer = await _context.Users.FindAsync(payment.PayerId);
            var memberIds = await _context.CycleMembers
                .Where(m => m.ExpenseCycleId == payment.ExpenseCycleId)
                .Select(m => m.UserId)
                .ToListAsync();

            if (memberIds.Count > 0)
                await _push.SendToUsersAsync(
                    memberIds,
                    NotificationType.CyclePaymentMade,
                    $"Payment made in {cycle.Name}",
                    $"{payer?.FirstName} {payer?.LastName} paid ${payment.Amount:F2} toward \"{cycle.Name}\".",
                    $"/cycles/{payment.ExpenseCycleId}",
                    payment.Id);
        }

        var dto = (await BuildDtosAsync(new List<Payment> { payment })).First();
        return (dto, null);
    }

    public async Task<string?> DeleteAsync(int paymentId, int requestingUserId)
    {
        var payment = await _context.Payments.FindAsync(paymentId);
        if (payment == null) return "Payment not found.";

        if (payment.PayerId != requestingUserId)
            return "Only the payer can delete a payment.";

        if (payment.Status == PaymentStatus.Confirmed)
            return "Cannot delete a confirmed payment.";

        _context.Payments.Remove(payment);
        await _context.SaveChangesAsync();
        return null;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<List<PaymentDto>> BuildDtosAsync(List<Payment> payments)
    {
        var userIds = payments.SelectMany(p => new[] { p.PayerId, p.PayeeId }).Distinct().ToList();
        var users   = await _context.Users.Where(u => userIds.Contains(u.Id)).ToListAsync();

        return payments.Select(p =>
        {
            var payer = users.FirstOrDefault(u => u.Id == p.PayerId);
            var payee = users.FirstOrDefault(u => u.Id == p.PayeeId);

            return new PaymentDto(
                p.Id,
                p.PayerId,
                payer?.FirstName ?? "",
                payer?.LastName  ?? "",
                p.PayeeId,
                payee?.FirstName ?? "",
                payee?.LastName  ?? "",
                p.ExpenseCycleId,
                p.Amount,
                p.Status.ToString(),
                p.Notes,
                p.CreatedAt,
                p.ConfirmedAt);
        }).ToList();
    }
}
