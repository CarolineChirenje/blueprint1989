using Microsoft.EntityFrameworkCore;
using Divvy.Api.Data;
using Divvy.Api.DTOs.Divvy;
using Divvy.Api.Models;

namespace Divvy.Api.Services;

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

        if (request.PayeeId == payerId)
            return (null, "Cannot create a payment to yourself.");

        var cycle = await _context.ExpenseCycles.FindAsync(request.ExpenseCycleId);
        if (cycle == null) return (null, "Expense cycle not found.");

        // Verify both payer and payee are members
        var payerIsMember = await _context.CycleMembers
            .AnyAsync(m => m.ExpenseCycleId == request.ExpenseCycleId && m.UserId == payerId);
        if (!payerIsMember) return (null, "You are not a member of this cycle.");

        var payeeIsMember = await _context.CycleMembers
            .AnyAsync(m => m.ExpenseCycleId == request.ExpenseCycleId && m.UserId == request.PayeeId);
        if (!payeeIsMember) return (null, "Payee is not a member of this cycle.");

        var payment = new Payment
        {
            PayerId        = payerId,
            PayeeId        = request.PayeeId,
            ExpenseCycleId = request.ExpenseCycleId,
            Amount         = Math.Round(request.Amount, 2),
            Status         = PaymentStatus.Pending,
            Notes          = request.Notes?.Trim(),
            CreatedAt      = DateTime.UtcNow
        };

        _context.Payments.Add(payment);
        await _context.SaveChangesAsync();

        // Notify payee that a payment is pending their confirmation
        var payer = await _context.Users.FindAsync(payerId);
        await _notifications.CreateAsync(
            request.PayeeId,
            $"{payer?.FirstName} {payer?.LastName} sent you a payment of ${payment.Amount:F2} — please confirm.",
            NotificationType.PaymentReceived,
            $"/cycles/{request.ExpenseCycleId}/payments",
            payment.Id);

        var dto = (await BuildDtosAsync(new List<Payment> { payment })).First();
        return (dto, null);
    }

    public async Task<(PaymentDto? dto, string? error)> RespondAsync(int paymentId, int respondingUserId, bool confirm, string? notes)
    {
        var payment = await _context.Payments.FindAsync(paymentId);
        if (payment == null) return (null, "Payment not found.");

        if (payment.PayeeId != respondingUserId)
            return (null, "Only the payee can confirm or reject a payment.");

        if (payment.Status != PaymentStatus.Pending)
            return (null, "Payment has already been responded to.");

        payment.Status      = confirm ? PaymentStatus.Confirmed : PaymentStatus.Rejected;
        payment.ConfirmedAt = confirm ? DateTime.UtcNow : null;
        if (!string.IsNullOrWhiteSpace(notes)) payment.Notes = notes.Trim();

        await _context.SaveChangesAsync();

        // Notify payer of the result
        var payee = await _context.Users.FindAsync(respondingUserId);
        var message = confirm
            ? $"{payee?.FirstName} confirmed your payment of ${payment.Amount:F2}."
            : $"{payee?.FirstName} rejected your payment of ${payment.Amount:F2}.";

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
            var cycle = await _context.ExpenseCycles.FindAsync(payment.ExpenseCycleId);
            var memberIds = await _context.CycleMembers
                .Where(m => m.ExpenseCycleId == payment.ExpenseCycleId)
                .Select(m => m.UserId)
                .ToListAsync();

            if (memberIds.Count > 0)
                await _push.SendToUsersAsync(
                    memberIds,
                    NotificationType.CyclePaymentMade,
                    $"Payment made in {cycle?.Name ?? "cycle"}",
                    $"{payer?.FirstName} {payer?.LastName} paid ${payment.Amount:F2} toward \"{cycle?.Name}\".",
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
