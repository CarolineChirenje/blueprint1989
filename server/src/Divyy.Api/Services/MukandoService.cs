using Microsoft.EntityFrameworkCore;
using Divvy.Api.Data;
using Divvy.Api.DTOs.Divvy;
using Divvy.Api.Models;

namespace Divvy.Api.Services;

public class MukandoService
{
    private readonly ApplicationDbContext    _context;
    private readonly IPushNotificationSender _push;

    public MukandoService(ApplicationDbContext context, IPushNotificationSender push)
    {
        _context = context;
        _push    = push;
    }

    // ── Round Queries ─────────────────────────────────────────────────────────

    public async Task<List<MukandoRoundDto>> GetRoundsAsync(int cycleId)
    {
        var rounds = await _context.MukandoRounds
            .Where(r => r.ExpenseCycleId == cycleId)
            .OrderBy(r => r.RoundNumber)
            .ToListAsync();

        var recipientIds = rounds.Select(r => r.RecipientUserId).Distinct().ToList();
        var users = await _context.Users
            .Where(u => recipientIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id);

        return rounds.Select(r =>
        {
            users.TryGetValue(r.RecipientUserId, out var user);
            return new MukandoRoundDto(
                r.Id, r.RoundNumber, r.RecipientUserId,
                user != null ? $"{user.FirstName} {user.LastName}" : "Unknown",
                r.Status.ToString(), r.ExpectedPool, r.ActualCollected,
                r.DueDate, r.PayoutConfirmed, null);
        }).ToList();
    }

    public async Task<MukandoRoundDto?> GetRoundDetailAsync(int roundId)
    {
        var round = await _context.MukandoRounds.FindAsync(roundId);
        if (round == null) return null;

        var recipient = await _context.Users.FindAsync(round.RecipientUserId);

        var contributions = await _context.MukandoContributions
            .Where(c => c.MukandoRoundId == roundId)
            .ToListAsync();

        var userIds = contributions.Select(c => c.UserId).Distinct().ToList();
        var users = await _context.Users
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id);

        var contributionDtos = contributions.Select(c =>
        {
            users.TryGetValue(c.UserId, out var user);
            return new MukandoContributionDto(
                c.Id, c.UserId,
                user?.FirstName ?? "Unknown", user?.LastName ?? "",
                c.Amount, c.Status.ToString(),
                c.ProofUrl, c.Reference, c.PaidAt, c.ConfirmedByAdminAt);
        }).ToList();

        return new MukandoRoundDto(
            round.Id, round.RoundNumber, round.RecipientUserId,
            recipient != null ? $"{recipient.FirstName} {recipient.LastName}" : "Unknown",
            round.Status.ToString(), round.ExpectedPool, round.ActualCollected,
            round.DueDate, round.PayoutConfirmed, contributionDtos);
    }

    public async Task<MukandoPayoutDto?> GetPayoutAsync(int roundId)
    {
        var payout = await _context.MukandoPayouts
            .FirstOrDefaultAsync(p => p.MukandoRoundId == roundId);
        if (payout == null) return null;

        var recipient = await _context.Users.FindAsync(payout.RecipientUserId);
        return new MukandoPayoutDto(
            payout.Id, payout.RecipientUserId,
            recipient != null ? $"{recipient.FirstName} {recipient.LastName}" : "Unknown",
            payout.AmountDisbursed, payout.PaymentMethod.ToString(),
            payout.ProofUrl, payout.Reference,
            payout.ConfirmedByUserId, payout.CreatedAt);
    }

    // ── Contribution Flow ─────────────────────────────────────────────────────

    public async Task<string?> RecordContributionAsync(int roundId, int userId, RecordContributionRequest request)
    {
        var round = await _context.MukandoRounds.FindAsync(roundId);
        if (round == null) return "Round not found.";
        if (round.Status != RoundStatus.Active) return "Round is not active.";
        if (round.RecipientUserId == userId) return "The recipient does not contribute to their own round.";

        if (string.IsNullOrWhiteSpace(request.ProofUrl))
            return "Proof of payment is required.";

        var contribution = await _context.MukandoContributions
            .FirstOrDefaultAsync(c => c.MukandoRoundId == roundId && c.UserId == userId);
        if (contribution == null) return "You are not a contributor for this round.";
        if (contribution.Status != ContributionStatus.Pending) return "Contribution already submitted or confirmed.";

        contribution.Status    = ContributionStatus.Paid;
        contribution.ProofUrl  = request.ProofUrl;
        contribution.Reference = request.Reference;
        contribution.Notes     = request.Notes;
        contribution.PaidAt    = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        // Log activity
        var user = await _context.Users.FindAsync(userId);
        await LogActivityAsync(roundId, userId, RoundActivityAction.ContributionSubmitted,
            $"{user?.FirstName} {user?.LastName} submitted contribution of {contribution.Amount:F2}");

        // Notify recipient + admins
        var cycle = await _context.ExpenseCycles.FindAsync(round.ExpenseCycleId);
        var currency = cycle != null ? await _context.Currencies.FindAsync(cycle.CurrencyId) : null;
        var sym = currency?.Symbol ?? "$";
        var adminIds = await GetCycleAdminIdsAsync(round.ExpenseCycleId);
        var notifyIds = new HashSet<int>(adminIds) { round.RecipientUserId };
        await _push.SendToUsersAsync(
            notifyIds.ToList(),
            NotificationType.MukandoContributionReceived,
            $"Contribution received: Round {round.RoundNumber}",
            $"{user?.FirstName} has submitted their {sym}{contribution.Amount:F2} contribution for Round {round.RoundNumber}.",
            $"/cycles/{round.ExpenseCycleId}",
            round.ExpenseCycleId);

        return null;
    }

    public async Task<string?> ConfirmContributionAsync(int roundId, int userId, int adminUserId)
    {
        var round = await _context.MukandoRounds.FindAsync(roundId);
        if (round == null) return "Round not found.";
        if (round.Status != RoundStatus.Active) return "Round is not active.";

        var contribution = await _context.MukandoContributions
            .FirstOrDefaultAsync(c => c.MukandoRoundId == roundId && c.UserId == userId);
        if (contribution == null) return "Contribution not found.";
        if (contribution.Status != ContributionStatus.Paid) return "Contribution must be in Paid status to confirm.";

        contribution.Status             = ContributionStatus.Confirmed;
        contribution.ConfirmedByAdminAt = DateTime.UtcNow;

        round.ActualCollected += contribution.Amount;
        round.UpdatedAt        = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        // Log activity
        var admin = await _context.Users.FindAsync(adminUserId);
        var member = await _context.Users.FindAsync(userId);
        await LogActivityAsync(roundId, adminUserId, RoundActivityAction.ContributionConfirmed,
            $"{admin?.FirstName} confirmed {member?.FirstName}'s contribution of {contribution.Amount:F2}");

        // Notify the contributing member
        await _push.SendToUserAsync(
            userId,
            NotificationType.MukandoContributionConfirmed,
            $"Contribution confirmed: Round {round.RoundNumber}",
            $"Your contribution for Round {round.RoundNumber} has been confirmed.",
            $"/cycles/{round.ExpenseCycleId}",
            round.ExpenseCycleId);

        // Check if all contributions collected
        var allConfirmed = !await _context.MukandoContributions
            .AnyAsync(c => c.MukandoRoundId == roundId && c.Status != ContributionStatus.Confirmed && c.Status != ContributionStatus.Missed);
        if (allConfirmed)
        {
            var adminIds = await GetCycleAdminIdsAsync(round.ExpenseCycleId);
            var cycle = await _context.ExpenseCycles.FindAsync(round.ExpenseCycleId);
            var currency = cycle != null ? await _context.Currencies.FindAsync(cycle.CurrencyId) : null;
            var sym = currency?.Symbol ?? "$";
            await _push.SendToUsersAsync(
                adminIds,
                NotificationType.MukandoAllContributionsCollected,
                $"All contributions collected: Round {round.RoundNumber}",
                $"All contributions for Round {round.RoundNumber} have been collected. {sym}{round.ActualCollected:F2} ready for payout.",
                $"/cycles/{round.ExpenseCycleId}",
                round.ExpenseCycleId);
        }

        return null;
    }

    // ── Payout Flow ───────────────────────────────────────────────────────────

    public async Task<string?> RecordPayoutAsync(int roundId, int adminUserId, RecordPayoutRequest request)
    {
        var round = await _context.MukandoRounds.FindAsync(roundId);
        if (round == null) return "Round not found.";
        if (round.Status != RoundStatus.Active) return "Round is not active.";
        if (round.PayoutConfirmed) return "Payout already recorded for this round.";

        if (string.IsNullOrWhiteSpace(request.ProofUrl))
            return "Proof of payout is required.";
        if (!Enum.TryParse<PaymentMethod>(request.PaymentMethod, true, out var method))
            return "Invalid payment method.";
        if (request.AmountDisbursed <= 0)
            return "Amount must be positive.";

        var payout = new MukandoPayout
        {
            MukandoRoundId    = roundId,
            RecipientUserId   = round.RecipientUserId,
            AmountDisbursed   = request.AmountDisbursed,
            PaymentMethod     = method,
            ProofUrl          = request.ProofUrl,
            Reference         = request.Reference,
            ConfirmedByUserId = adminUserId,
            CreatedAt         = DateTime.UtcNow
        };

        _context.MukandoPayouts.Add(payout);

        round.PayoutConfirmed   = true;
        round.PayoutConfirmedAt = DateTime.UtcNow;
        round.Status            = RoundStatus.Completed;
        round.UpdatedAt         = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        // Log activity
        var admin = await _context.Users.FindAsync(adminUserId);
        await LogActivityAsync(roundId, adminUserId, RoundActivityAction.PayoutRecorded,
            $"{admin?.FirstName} recorded payout of {request.AmountDisbursed:F2} to recipient");

        // Notify recipient
        var cycle = await _context.ExpenseCycles.FindAsync(round.ExpenseCycleId);
        var currency = cycle != null ? await _context.Currencies.FindAsync(cycle.CurrencyId) : null;
        var sym = currency?.Symbol ?? "$";
        await _push.SendToUserAsync(
            round.RecipientUserId,
            NotificationType.MukandoPayoutConfirmed,
            $"Payout confirmed: Round {round.RoundNumber}",
            $"Your payout of {sym}{request.AmountDisbursed:F2} for Round {round.RoundNumber} has been confirmed!",
            $"/cycles/{round.ExpenseCycleId}",
            round.ExpenseCycleId);

        // Notify all members that round is complete
        var memberIds = await _context.CycleMembers
            .Where(m => m.ExpenseCycleId == round.ExpenseCycleId)
            .Select(m => m.UserId).ToListAsync();

        await _push.SendToUsersAsync(
            memberIds,
            NotificationType.MukandoRoundCompleted,
            $"Round {round.RoundNumber} complete",
            $"Round {round.RoundNumber} of \"{cycle?.Name}\" is complete.",
            $"/cycles/{round.ExpenseCycleId}",
            round.ExpenseCycleId);

        // Activate next round or complete cycle
        await ActivateNextRoundOrCompleteCycleAsync(round.ExpenseCycleId);

        return null;
    }

    public async Task<string?> ForceCloseRoundAsync(int roundId, int adminUserId)
    {
        var round = await _context.MukandoRounds.FindAsync(roundId);
        if (round == null) return "Round not found.";
        if (round.Status != RoundStatus.Active) return "Round is not active.";

        // Mark all unpaid contributions as Missed
        var unpaid = await _context.MukandoContributions
            .Where(c => c.MukandoRoundId == roundId && (c.Status == ContributionStatus.Pending || c.Status == ContributionStatus.Paid))
            .ToListAsync();

        foreach (var c in unpaid)
        {
            if (c.Status == ContributionStatus.Pending)
                c.Status = ContributionStatus.Missed;
            // Paid but unconfirmed: admin should confirm individually first, or they become missed too
            if (c.Status == ContributionStatus.Paid)
                c.Status = ContributionStatus.Missed;
        }

        await _context.SaveChangesAsync();

        // Log activity
        var admin = await _context.Users.FindAsync(adminUserId);
        await LogActivityAsync(roundId, adminUserId, RoundActivityAction.RoundForceClose,
            $"{admin?.FirstName} force-closed Round {round.RoundNumber}. {unpaid.Count} contribution(s) marked as missed.");

        return null; // Admin must still record payout separately
    }

    private async Task ActivateNextRoundOrCompleteCycleAsync(int cycleId)
    {
        var nextRound = await _context.MukandoRounds
            .Where(r => r.ExpenseCycleId == cycleId && r.Status == RoundStatus.Pending)
            .OrderBy(r => r.RoundNumber)
            .FirstOrDefaultAsync();

        if (nextRound != null)
        {
            nextRound.Status    = RoundStatus.Active;
            nextRound.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            await LogActivityAsync(nextRound.Id, 0, RoundActivityAction.RoundActivated,
                $"Round {nextRound.RoundNumber} activated automatically");

            var cycle = await _context.ExpenseCycles.FindAsync(cycleId);
            var recipient = await _context.Users.FindAsync(nextRound.RecipientUserId);
            var currency = cycle != null ? await _context.Currencies.FindAsync(cycle.CurrencyId) : null;
            var sym = currency?.Symbol ?? "$";
            var recipientName = recipient != null ? $"{recipient.FirstName} {recipient.LastName}" : "Unknown";
            var memberIds = await _context.CycleMembers
                .Where(m => m.ExpenseCycleId == cycleId).Select(m => m.UserId).ToListAsync();

            await _push.SendToUsersAsync(
                memberIds,
                NotificationType.MukandoRoundStarted,
                $"Round {nextRound.RoundNumber} started",
                $"Round {nextRound.RoundNumber} has started. {recipientName} receives this round. Contribute {sym}{cycle?.ContributionAmount:F2} by {nextRound.DueDate:MMM d, yyyy}.",
                $"/cycles/{cycleId}",
                cycleId);
        }
        else
        {
            // All rounds done — close the cycle
            var cycle = await _context.ExpenseCycles.FindAsync(cycleId);
            if (cycle != null && cycle.Status == CycleStatus.Active)
            {
                cycle.Status    = CycleStatus.Closed;
                cycle.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                var memberIds = await _context.CycleMembers
                    .Where(m => m.ExpenseCycleId == cycleId).Select(m => m.UserId).ToListAsync();

                await _push.SendToUsersAsync(
                    memberIds,
                    NotificationType.MukandoCycleCompleted,
                    $"Mukando complete: {cycle.Name}",
                    $"All rounds in \"{cycle.Name}\" are complete! The cycle is now closed.",
                    $"/cycles/{cycleId}",
                    cycleId);
            }
        }
    }

    // ── Mukando Settings (Draft editing) ──────────────────────────────────────

    public async Task<string?> UpdateSettingsAsync(int cycleId, UpdateMukandoSettingsRequest request)
    {
        var cycle = await _context.ExpenseCycles.FindAsync(cycleId);
        if (cycle == null) return "Cycle not found.";
        if (cycle.CycleType != CycleType.Mukando) return "Only Mukando cycles have these settings.";
        if (cycle.Status != CycleStatus.Draft) return "Settings can only be changed in Draft mode.";

        if (request.ContributionAmount <= 0)
            return "Contribution amount must be positive.";
        if (!Enum.TryParse<CycleFrequency>(request.Frequency, true, out var freq))
            return "Invalid frequency.";

        var memberIds = await _context.CycleMembers
            .Where(m => m.ExpenseCycleId == cycleId).Select(m => m.UserId).ToHashSetAsync();
        var payoutSet = new HashSet<int>(request.PayoutOrder);
        if (!payoutSet.SetEquals(memberIds))
            return "Payout order must match the current member list.";
        if (request.PayoutOrder.Count != request.PayoutOrder.Distinct().Count())
            return "Payout order must not contain duplicates.";

        // Update cycle
        cycle.ContributionAmount = request.ContributionAmount;
        cycle.Frequency          = freq;
        cycle.UpdatedAt          = DateTime.UtcNow;

        // Delete swap requests first (FK RESTRICT prevents round deletion otherwise)
        var existingSwaps = await _context.MukandoSwapRequests
            .Where(s => s.ExpenseCycleId == cycleId).ToListAsync();
        _context.MukandoSwapRequests.RemoveRange(existingSwaps);

        // Delete existing rounds and contributions (cascade handles contributions)
        var existingRounds = await _context.MukandoRounds
            .Where(r => r.ExpenseCycleId == cycleId).ToListAsync();
        _context.MukandoRounds.RemoveRange(existingRounds);
        await _context.SaveChangesAsync();

        // Regenerate rounds
        int memberCount = memberIds.Count;
        decimal expectedPool = request.ContributionAmount * (memberCount - 1);

        for (int i = 0; i < request.PayoutOrder.Count; i++)
        {
            var recipientId = request.PayoutOrder[i];
            var dueDate = ExpenseCycleService.CalculateRoundDueDate(cycle.StartDate, freq, i);

            var round = new MukandoRound
            {
                ExpenseCycleId  = cycleId,
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

            foreach (var uid in memberIds.Where(m => m != recipientId))
            {
                _context.MukandoContributions.Add(new MukandoContribution
                {
                    MukandoRoundId = round.Id,
                    UserId         = uid,
                    Amount         = request.ContributionAmount,
                    Status         = ContributionStatus.Pending,
                    CreatedAt      = DateTime.UtcNow
                });
            }
        }

        cycle.EndDate = ExpenseCycleService.CalculateRoundDueDate(cycle.StartDate, freq, request.PayoutOrder.Count - 1);
        await _context.SaveChangesAsync();

        return null;
    }

    /// <summary>
    /// Regenerates Mukando rounds after a member is added or removed in Draft mode.
    /// Preserves the existing payout order for remaining members and appends any new member.
    /// </summary>
    public async Task RegenerateRoundsAfterMemberChangeAsync(int cycleId)
    {
        var cycle = await _context.ExpenseCycles.FindAsync(cycleId);
        if (cycle == null || cycle.CycleType != CycleType.Mukando || cycle.Status != CycleStatus.Draft)
            return;
        if (!cycle.ContributionAmount.HasValue || !cycle.Frequency.HasValue)
            return;

        var memberIds = await _context.CycleMembers
            .Where(m => m.ExpenseCycleId == cycleId).Select(m => m.UserId).ToListAsync();
        if (memberIds.Count < 2) return;

        // Preserve existing payout order where possible
        var existingRounds = await _context.MukandoRounds
            .Where(r => r.ExpenseCycleId == cycleId)
            .OrderBy(r => r.RoundNumber)
            .ToListAsync();

        var orderedIds = existingRounds
            .Select(r => r.RecipientUserId)
            .Where(id => memberIds.Contains(id))
            .ToList();

        // Append any new members not yet in the payout order
        foreach (var id in memberIds.Where(id => !orderedIds.Contains(id)))
            orderedIds.Add(id);

        // Delete swap requests first (FK RESTRICT)
        var swaps = await _context.MukandoSwapRequests
            .Where(s => s.ExpenseCycleId == cycleId).ToListAsync();
        _context.MukandoSwapRequests.RemoveRange(swaps);
        await _context.SaveChangesAsync();

        // Delete existing rounds (cascade handles contributions)
        _context.MukandoRounds.RemoveRange(existingRounds);
        await _context.SaveChangesAsync();

        // Recreate rounds
        int memberCount = orderedIds.Count;
        decimal expectedPool = cycle.ContributionAmount.Value * (memberCount - 1);

        for (int i = 0; i < orderedIds.Count; i++)
        {
            var recipientId = orderedIds[i];
            var dueDate = ExpenseCycleService.CalculateRoundDueDate(cycle.StartDate, cycle.Frequency.Value, i);

            var round = new MukandoRound
            {
                ExpenseCycleId  = cycleId,
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

            foreach (var uid in orderedIds.Where(m => m != recipientId))
            {
                _context.MukandoContributions.Add(new MukandoContribution
                {
                    MukandoRoundId = round.Id,
                    UserId         = uid,
                    Amount         = cycle.ContributionAmount.Value,
                    Status         = ContributionStatus.Pending,
                    CreatedAt      = DateTime.UtcNow
                });
            }
        }

        cycle.EndDate = ExpenseCycleService.CalculateRoundDueDate(cycle.StartDate, cycle.Frequency.Value, orderedIds.Count - 1);
        await _context.SaveChangesAsync();
    }

    // ── Swap Requests ─────────────────────────────────────────────────────────

    public async Task<List<MukandoSwapRequestDto>> GetSwapRequestsAsync(int cycleId)
    {
        var swaps = await _context.MukandoSwapRequests
            .Where(s => s.ExpenseCycleId == cycleId)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();

        var userIds = swaps.SelectMany(s => new[] { s.RequesterUserId, s.TargetUserId }).Distinct().ToList();
        var users = await _context.Users.Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id);

        var roundIds = swaps.SelectMany(s => new[] { s.RequesterRoundId, s.TargetRoundId }).Distinct().ToList();
        var rounds = await _context.MukandoRounds.Where(r => roundIds.Contains(r.Id)).ToDictionaryAsync(r => r.Id);

        return swaps.Select(s =>
        {
            users.TryGetValue(s.RequesterUserId, out var req);
            users.TryGetValue(s.TargetUserId, out var tgt);
            rounds.TryGetValue(s.RequesterRoundId, out var reqRound);
            rounds.TryGetValue(s.TargetRoundId, out var tgtRound);
            return new MukandoSwapRequestDto(
                s.Id,
                s.RequesterUserId, req != null ? $"{req.FirstName} {req.LastName}" : "Unknown",
                reqRound?.RoundNumber ?? 0,
                s.TargetUserId, tgt != null ? $"{tgt.FirstName} {tgt.LastName}" : "Unknown",
                tgtRound?.RoundNumber ?? 0,
                s.Status.ToString(), s.CreatedAt, s.RespondedAt);
        }).ToList();
    }

    public async Task<(MukandoSwapRequestDto? dto, string? error)> CreateSwapRequestAsync(int cycleId, int requesterUserId, int targetUserId)
    {
        var cycle = await _context.ExpenseCycles.FindAsync(cycleId);
        if (cycle == null) return (null, "Cycle not found.");
        if (cycle.CycleType != CycleType.Mukando) return (null, "Swaps are only for Mukando cycles.");
        if (cycle.Status != CycleStatus.Draft) return (null, "Swaps can only be requested in Draft mode.");
        if (requesterUserId == targetUserId) return (null, "Cannot swap with yourself.");

        // Check both are members
        var isMember = await _context.CycleMembers
            .AnyAsync(m => m.ExpenseCycleId == cycleId && m.UserId == targetUserId);
        if (!isMember) return (null, "Target user is not a member of this cycle.");

        // Check no pending swap for requester
        var hasPending = await _context.MukandoSwapRequests
            .AnyAsync(s => s.ExpenseCycleId == cycleId && s.RequesterUserId == requesterUserId && s.Status == SwapRequestStatus.Pending);
        if (hasPending) return (null, "You already have a pending swap request.");

        // Find the rounds
        var requesterRound = await _context.MukandoRounds
            .FirstOrDefaultAsync(r => r.ExpenseCycleId == cycleId && r.RecipientUserId == requesterUserId);
        var targetRound = await _context.MukandoRounds
            .FirstOrDefaultAsync(r => r.ExpenseCycleId == cycleId && r.RecipientUserId == targetUserId);
        if (requesterRound == null || targetRound == null)
            return (null, "Could not find rounds for both users.");

        var swap = new MukandoSwapRequest
        {
            ExpenseCycleId   = cycleId,
            RequesterUserId  = requesterUserId,
            RequesterRoundId = requesterRound.Id,
            TargetUserId     = targetUserId,
            TargetRoundId    = targetRound.Id,
            Status           = SwapRequestStatus.Pending,
            CreatedAt        = DateTime.UtcNow
        };

        _context.MukandoSwapRequests.Add(swap);
        await _context.SaveChangesAsync();

        // Log and notify
        var requester = await _context.Users.FindAsync(requesterUserId);
        await LogActivityAsync(requesterRound.Id, requesterUserId, RoundActivityAction.SwapRequested,
            $"{requester?.FirstName} requested to swap Round {requesterRound.RoundNumber} with Round {targetRound.RoundNumber}");

        await _push.SendToUserAsync(
            targetUserId,
            NotificationType.MukandoSwapRequested,
            $"Swap request: {cycle.Name}",
            $"{requester?.FirstName} wants to swap Round {requesterRound.RoundNumber} with your Round {targetRound.RoundNumber}.",
            $"/cycles/{cycleId}",
            cycleId);

        var swaps = await GetSwapRequestsAsync(cycleId);
        return (swaps.FirstOrDefault(s => s.Id == swap.Id), null);
    }

    public async Task<string?> RespondSwapRequestAsync(int swapId, int respondingUserId, bool accept)
    {
        var swap = await _context.MukandoSwapRequests.FindAsync(swapId);
        if (swap == null) return "Swap request not found.";
        if (swap.Status != SwapRequestStatus.Pending) return "This swap request is no longer pending.";
        if (swap.TargetUserId != respondingUserId) return "Only the target user can respond to this swap.";

        var cycle = await _context.ExpenseCycles.FindAsync(swap.ExpenseCycleId);
        if (cycle?.Status != CycleStatus.Draft) return "Swaps can only be processed in Draft mode.";

        swap.RespondedAt = DateTime.UtcNow;

        if (accept)
        {
            swap.Status = SwapRequestStatus.Accepted;

            // Perform the swap
            var requesterRound = await _context.MukandoRounds.FindAsync(swap.RequesterRoundId);
            var targetRound = await _context.MukandoRounds.FindAsync(swap.TargetRoundId);
            if (requesterRound != null && targetRound != null)
            {
                (requesterRound.RecipientUserId, targetRound.RecipientUserId) =
                    (targetRound.RecipientUserId, requesterRound.RecipientUserId);

                // Regenerate contributions for both affected rounds
                await RegenerateRoundContributionsAsync(requesterRound, cycle.ContributionAmount ?? 0);
                await RegenerateRoundContributionsAsync(targetRound, cycle.ContributionAmount ?? 0);

                var memberIds = await _context.CycleMembers
                    .Where(m => m.ExpenseCycleId == swap.ExpenseCycleId).Select(m => m.UserId).ToHashSetAsync();
            }

            await _context.SaveChangesAsync();

            await LogActivityAsync(swap.RequesterRoundId, respondingUserId, RoundActivityAction.SwapAccepted,
                $"Swap accepted between Round {requesterRound?.RoundNumber} and Round {targetRound?.RoundNumber}");

            // Notify requester + admins
            var adminIds = await GetCycleAdminIdsAsync(swap.ExpenseCycleId);
            var notifyIds = new HashSet<int>(adminIds) { swap.RequesterUserId };
            await _push.SendToUsersAsync(
                notifyIds.ToList(),
                NotificationType.MukandoSwapAccepted,
                $"Swap accepted: {cycle.Name}",
                $"Turn swap accepted! Rounds have been updated.",
                $"/cycles/{swap.ExpenseCycleId}",
                swap.ExpenseCycleId);
        }
        else
        {
            swap.Status = SwapRequestStatus.Declined;
            await _context.SaveChangesAsync();

            await LogActivityAsync(swap.RequesterRoundId, respondingUserId, RoundActivityAction.SwapDeclined,
                $"Swap request declined");

            await _push.SendToUserAsync(
                swap.RequesterUserId,
                NotificationType.MukandoSwapDeclined,
                $"Swap declined: {cycle.Name}",
                "Your swap request has been declined.",
                $"/cycles/{swap.ExpenseCycleId}",
                swap.ExpenseCycleId);
        }

        return null;
    }

    public async Task<string?> CancelSwapRequestAsync(int swapId, int userId)
    {
        var swap = await _context.MukandoSwapRequests.FindAsync(swapId);
        if (swap == null) return "Swap request not found.";
        if (swap.Status != SwapRequestStatus.Pending) return "Only pending swaps can be cancelled.";
        if (swap.RequesterUserId != userId) return "Only the requester can cancel a swap.";

        swap.Status      = SwapRequestStatus.Cancelled;
        swap.RespondedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return null;
    }

    private async Task RegenerateRoundContributionsAsync(MukandoRound round, decimal contributionAmount)
    {
        var existing = await _context.MukandoContributions
            .Where(c => c.MukandoRoundId == round.Id).ToListAsync();
        _context.MukandoContributions.RemoveRange(existing);
        await _context.SaveChangesAsync();

        var memberIds = await _context.CycleMembers
            .Where(m => m.ExpenseCycleId == round.ExpenseCycleId)
            .Select(m => m.UserId).ToListAsync();

        foreach (var uid in memberIds.Where(m => m != round.RecipientUserId))
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

    // ── Opt-out Requests ──────────────────────────────────────────────────────

    public async Task<List<MukandoOptOutRequestDto>> GetOptOutRequestsAsync(int cycleId)
    {
        var requests = await _context.MukandoOptOutRequests
            .Where(o => o.ExpenseCycleId == cycleId)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();

        var userIds = requests.Select(o => o.UserId).Distinct().ToList();
        var users = await _context.Users.Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id);

        return requests.Select(o =>
        {
            users.TryGetValue(o.UserId, out var user);
            return new MukandoOptOutRequestDto(
                o.Id, o.UserId,
                user != null ? $"{user.FirstName} {user.LastName}" : "Unknown",
                o.Reason, o.Status.ToString(), o.CreatedAt, o.RespondedAt);
        }).ToList();
    }

    public async Task<(MukandoOptOutRequestDto? dto, string? error)> CreateOptOutRequestAsync(int cycleId, int userId, string reason)
    {
        var cycle = await _context.ExpenseCycles.FindAsync(cycleId);
        if (cycle == null) return (null, "Cycle not found.");
        if (cycle.Status != CycleStatus.Draft) return (null, "Opt-out requests can only be made in Draft mode.");

        var isMember = await _context.CycleMembers.AnyAsync(m => m.ExpenseCycleId == cycleId && m.UserId == userId);
        if (!isMember) return (null, "You are not a member of this cycle.");

        var hasPending = await _context.MukandoOptOutRequests
            .AnyAsync(o => o.ExpenseCycleId == cycleId && o.UserId == userId && o.Status == OptOutRequestStatus.Pending);
        if (hasPending) return (null, "You already have a pending opt-out request.");

        var req = new MukandoOptOutRequest
        {
            ExpenseCycleId = cycleId,
            UserId         = userId,
            Reason         = reason,
            Status         = OptOutRequestStatus.Pending,
            CreatedAt      = DateTime.UtcNow
        };
        _context.MukandoOptOutRequests.Add(req);
        await _context.SaveChangesAsync();

        // Notify admins
        var user = await _context.Users.FindAsync(userId);
        var adminIds = await GetCycleAdminIdsAsync(cycleId);
        await _push.SendToUsersAsync(
            adminIds,
            NotificationType.MukandoOptOutRequested,
            $"Opt-out request: {cycle.Name}",
            $"{user?.FirstName} has requested to leave the cycle \"{cycle.Name}\".",
            $"/cycles/{cycleId}",
            cycleId);

        var requests = await GetOptOutRequestsAsync(cycleId);
        return (requests.FirstOrDefault(o => o.Id == req.Id), null);
    }

    public async Task<string?> RespondOptOutRequestAsync(int requestId, int adminUserId, bool approve)
    {
        var req = await _context.MukandoOptOutRequests.FindAsync(requestId);
        if (req == null) return "Opt-out request not found.";
        if (req.Status != OptOutRequestStatus.Pending) return "Request is no longer pending.";

        var cycle = await _context.ExpenseCycles.FindAsync(req.ExpenseCycleId);
        if (cycle?.Status != CycleStatus.Draft) return "Opt-out can only be approved in Draft mode.";

        req.Status            = approve ? OptOutRequestStatus.Approved : OptOutRequestStatus.Rejected;
        req.RespondedByUserId = adminUserId;
        req.RespondedAt       = DateTime.UtcNow;

        if (approve)
        {
            // Remove member from cycle
            var member = await _context.CycleMembers
                .FirstOrDefaultAsync(m => m.ExpenseCycleId == req.ExpenseCycleId && m.UserId == req.UserId);
            if (member != null) _context.CycleMembers.Remove(member);

            // Cancel any pending swap requests involving this user
            var swaps = await _context.MukandoSwapRequests
                .Where(s => s.ExpenseCycleId == req.ExpenseCycleId
                         && s.Status == SwapRequestStatus.Pending
                         && (s.RequesterUserId == req.UserId || s.TargetUserId == req.UserId))
                .ToListAsync();
            foreach (var swap in swaps)
            {
                swap.Status      = SwapRequestStatus.Cancelled;
                swap.RespondedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();

            // Regenerate all rounds with remaining members
            var memberIds = await _context.CycleMembers
                .Where(m => m.ExpenseCycleId == req.ExpenseCycleId).Select(m => m.UserId).ToListAsync();

            if (memberIds.Count >= 2 && cycle.ContributionAmount.HasValue && cycle.Frequency.HasValue)
            {
                // Delete all swap requests referencing rounds (FK RESTRICT prevents round deletion otherwise)
                var allSwaps = await _context.MukandoSwapRequests
                    .Where(s => s.ExpenseCycleId == req.ExpenseCycleId).ToListAsync();
                _context.MukandoSwapRequests.RemoveRange(allSwaps);
                await _context.SaveChangesAsync();

                // Remove old rounds
                var oldRounds = await _context.MukandoRounds
                    .Where(r => r.ExpenseCycleId == req.ExpenseCycleId).ToListAsync();
                _context.MukandoRounds.RemoveRange(oldRounds);
                await _context.SaveChangesAsync();

                // Recreate rounds for remaining members
                int memberCount = memberIds.Count;
                decimal expectedPool = cycle.ContributionAmount.Value * (memberCount - 1);

                for (int i = 0; i < memberCount; i++)
                {
                    var recipientId = memberIds[i]; // maintain order
                    var dueDate = ExpenseCycleService.CalculateRoundDueDate(cycle.StartDate, cycle.Frequency.Value, i);

                    var round = new MukandoRound
                    {
                        ExpenseCycleId  = req.ExpenseCycleId,
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

                    foreach (var uid in memberIds.Where(m => m != recipientId))
                    {
                        _context.MukandoContributions.Add(new MukandoContribution
                        {
                            MukandoRoundId = round.Id,
                            UserId         = uid,
                            Amount         = cycle.ContributionAmount.Value,
                            Status         = ContributionStatus.Pending,
                            CreatedAt      = DateTime.UtcNow
                        });
                    }
                }

                cycle.EndDate = ExpenseCycleService.CalculateRoundDueDate(cycle.StartDate, cycle.Frequency.Value, memberCount - 1);
            }
        }

        await _context.SaveChangesAsync();

        // Notify the member
        var notifType = approve ? NotificationType.MukandoOptOutResponded : NotificationType.MukandoOptOutResponded;
        var message = approve
            ? $"Your request to leave \"{cycle.Name}\" has been approved."
            : $"Your request to leave \"{cycle.Name}\" has been declined.";

        await _push.SendToUserAsync(req.UserId, notifType, $"Opt-out {(approve ? "approved" : "declined")}",
            message, $"/cycles/{req.ExpenseCycleId}", req.ExpenseCycleId);

        return null;
    }

    // ── Activity Log ──────────────────────────────────────────────────────────

    public async Task<List<MukandoRoundActivityDto>> GetRoundActivitiesAsync(int roundId)
    {
        var activities = await _context.MukandoRoundActivities
            .Where(a => a.MukandoRoundId == roundId)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();

        var userIds = activities.Select(a => a.UserId).Where(id => id > 0).Distinct().ToList();
        var users = userIds.Any()
            ? await _context.Users.Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id)
            : new Dictionary<int, User>();

        return activities.Select(a =>
        {
            users.TryGetValue(a.UserId, out var user);
            return new MukandoRoundActivityDto(
                a.Id, a.Action.ToString(), a.Details, a.UserId,
                user != null ? $"{user.FirstName} {user.LastName}" : "System",
                a.CreatedAt);
        }).ToList();
    }

    private async Task LogActivityAsync(int roundId, int userId, RoundActivityAction action, string details)
    {
        _context.MukandoRoundActivities.Add(new MukandoRoundActivity
        {
            MukandoRoundId = roundId,
            UserId         = userId,
            Action         = action,
            Details        = details,
            CreatedAt      = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();
    }

    // ── Mukando Summary & Reliability ─────────────────────────────────────────

    public async Task<MukandoCycleSummaryDto?> GetCycleSummaryAsync(int cycleId)
    {
        var cycle = await _context.ExpenseCycles.FindAsync(cycleId);
        if (cycle == null || cycle.CycleType != CycleType.Mukando) return null;

        var rounds = await _context.MukandoRounds
            .Where(r => r.ExpenseCycleId == cycleId).ToListAsync();

        var totalDisbursed = await _context.MukandoPayouts
            .Where(p => rounds.Select(r => r.Id).Contains(p.MukandoRoundId))
            .SumAsync(p => p.AmountDisbursed);

        var totalCollected = rounds.Sum(r => r.ActualCollected);
        var totalExpectedPool = rounds.Sum(r => r.ExpectedPool);
        var roundsCompleted = rounds.Count(r => r.Status == RoundStatus.Completed);

        // Member reliability
        var allContributions = await _context.MukandoContributions
            .Where(c => rounds.Select(r => r.Id).Contains(c.MukandoRoundId))
            .ToListAsync();

        var memberIds = await _context.CycleMembers
            .Where(m => m.ExpenseCycleId == cycleId).Select(m => m.UserId).ToListAsync();
        var users = await _context.Users.Where(u => memberIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id);

        var totalContributions = allContributions.Count;
        var onTimeTotal = allContributions.Count(c => c.Status == ContributionStatus.Confirmed);
        var onTimeRate = totalContributions > 0 ? Math.Round((decimal)onTimeTotal / totalContributions * 100, 1) : 100m;

        var reliability = memberIds.Select(uid =>
        {
            users.TryGetValue(uid, out var user);
            var memberContribs = allContributions.Where(c => c.UserId == uid).ToList();
            var onTime = memberContribs.Count(c => c.Status == ContributionStatus.Confirmed);
            var missed = memberContribs.Count(c => c.Status == ContributionStatus.Missed);
            var total = memberContribs.Count;
            var pct = total > 0 ? Math.Round((decimal)onTime / total * 100, 1) : 100m;
            return new MemberReliabilityDto(uid,
                user?.FirstName ?? "Unknown", user?.LastName ?? "",
                onTime, missed, total, pct);
        }).ToList();

        return new MukandoCycleSummaryDto(
            cycleId, cycle.Name, totalDisbursed, totalCollected, totalExpectedPool,
            roundsCompleted, rounds.Count, onTimeRate, reliability);
    }

    // ── Export ─────────────────────────────────────────────────────────────────

    public async Task<string?> ExportCycleCsvAsync(int cycleId)
    {
        var cycle = await _context.ExpenseCycles.FindAsync(cycleId);
        if (cycle == null || cycle.CycleType != CycleType.Mukando) return null;

        var rounds = await _context.MukandoRounds
            .Where(r => r.ExpenseCycleId == cycleId).OrderBy(r => r.RoundNumber).ToListAsync();

        var allContributions = await _context.MukandoContributions
            .Where(c => rounds.Select(r => r.Id).Contains(c.MukandoRoundId)).ToListAsync();

        var payouts = await _context.MukandoPayouts
            .Where(p => rounds.Select(r => r.Id).Contains(p.MukandoRoundId)).ToListAsync();

        var userIds = allContributions.Select(c => c.UserId)
            .Union(rounds.Select(r => r.RecipientUserId)).Distinct().ToList();
        var users = await _context.Users.Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id);

        var currency = await _context.Currencies.FindAsync(cycle.CurrencyId);
        var sym = currency?.Symbol ?? "$";

        var lines = new List<string>
        {
            "Round,Recipient,DueDate,Status,ExpectedPool,ActualCollected,PayoutAmount,PayoutMethod,MemberContributions"
        };

        foreach (var round in rounds)
        {
            users.TryGetValue(round.RecipientUserId, out var recipient);
            var payout = payouts.FirstOrDefault(p => p.MukandoRoundId == round.Id);
            var contribs = allContributions.Where(c => c.MukandoRoundId == round.Id).ToList();

            var contribSummary = string.Join("; ", contribs.Select(c =>
            {
                users.TryGetValue(c.UserId, out var u);
                return $"{u?.FirstName ?? "?"}: {c.Status}";
            }));

            lines.Add($"{round.RoundNumber}," +
                       $"{recipient?.FirstName} {recipient?.LastName}," +
                       $"{round.DueDate:yyyy-MM-dd}," +
                       $"{round.Status}," +
                       $"{sym}{round.ExpectedPool:F2}," +
                       $"{sym}{round.ActualCollected:F2}," +
                       $"{(payout != null ? $"{sym}{payout.AmountDisbursed:F2}" : "")}," +
                       $"{payout?.PaymentMethod.ToString() ?? ""}," +
                       $"\"{contribSummary}\"");
        }

        return string.Join(Environment.NewLine, lines);
    }

    // ── Dashboard ─────────────────────────────────────────────────────────────

    public async Task<MukandoDashboardDto> GetDashboardAsync(int userId)
    {
        // Find user's active Mukando cycles
        var cycleIds = await _context.CycleMembers
            .Where(m => m.UserId == userId).Select(m => m.ExpenseCycleId).ToListAsync();

        var activeMukandoCycles = await _context.ExpenseCycles
            .Where(c => cycleIds.Contains(c.Id) && c.Status == CycleStatus.Active && c.CycleType == CycleType.Mukando)
            .ToListAsync();

        MukandoNextContributionDto? nextContribution = null;
        MukandoPayoutRoundDto? payoutRound = null;
        MukandoActiveRoundStatusDto? activeRoundStatus = null;

        foreach (var cycle in activeMukandoCycles)
        {
            var activeRound = await _context.MukandoRounds
                .FirstOrDefaultAsync(r => r.ExpenseCycleId == cycle.Id && r.Status == RoundStatus.Active);
            if (activeRound == null) continue;

            // Next contribution (if user has pending contribution for this round)
            var myContrib = await _context.MukandoContributions
                .FirstOrDefaultAsync(c => c.MukandoRoundId == activeRound.Id && c.UserId == userId && c.Status == ContributionStatus.Pending);
            if (myContrib != null && nextContribution == null)
            {
                var recipient = await _context.Users.FindAsync(activeRound.RecipientUserId);
                var currency = await _context.Currencies.FindAsync(cycle.CurrencyId);
                nextContribution = new MukandoNextContributionDto(
                    cycle.Id, cycle.Name, myContrib.Amount, currency?.Symbol ?? "$",
                    activeRound.DueDate, activeRound.RoundNumber,
                    recipient != null ? $"{recipient.FirstName} {recipient.LastName}" : "Unknown");
            }

            // Payout round
            if (payoutRound == null)
            {
                var myRound = await _context.MukandoRounds
                    .FirstOrDefaultAsync(r => r.ExpenseCycleId == cycle.Id && r.RecipientUserId == userId && r.Status != RoundStatus.Completed);
                if (myRound != null)
                {
                    payoutRound = new MukandoPayoutRoundDto(cycle.Id, cycle.Name, myRound.RoundNumber, myRound.DueDate);
                }
            }

            // Active round status
            if (activeRoundStatus == null)
            {
                var confirmed = await _context.MukandoContributions
                    .CountAsync(c => c.MukandoRoundId == activeRound.Id && c.Status == ContributionStatus.Confirmed);
                var total = await _context.MukandoContributions
                    .CountAsync(c => c.MukandoRoundId == activeRound.Id);
                activeRoundStatus = new MukandoActiveRoundStatusDto(
                    cycle.Id, cycle.Name, activeRound.RoundNumber, confirmed, total);
            }
        }

        return new MukandoDashboardDto(nextContribution, payoutRound, activeRoundStatus);
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private async Task<List<int>> GetCycleAdminIdsAsync(int cycleId)
    {
        var groupId = await _context.ExpenseCycles
            .Where(c => c.Id == cycleId).Select(c => c.GroupId).FirstOrDefaultAsync();

        return await _context.GroupMembers
            .Where(gm => gm.GroupId == groupId && gm.GroupRole == GroupRole.GroupAdmin && gm.Status == GroupInviteStatus.Accepted)
            .Select(gm => gm.UserId)
            .ToListAsync();
    }
}
