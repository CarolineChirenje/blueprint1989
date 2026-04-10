using Microsoft.EntityFrameworkCore;
using Batanai.Api.Data;
using Batanai.Api.DTOs.Batanai;
using Batanai.Api.Models;
using System.Security.Cryptography;

namespace Batanai.Api.Services;

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

        contribution.ProofUrl  = request.ProofUrl;
        contribution.Reference = request.Reference;
        contribution.Notes     = request.Notes;
        contribution.PaidAt    = DateTime.UtcNow;

        // ── Admin-pays guard ──────────────────────────────────────────────────
        // If the contributor is a GroupAdmin of this cycle's group, they cannot
        // self-confirm. The system immediately assigns a random independent verifier
        // so no admin can touch the confirmation step.
        bool contributorIsAdmin = await IsGroupAdminOfCycleAsync(round.ExpenseCycleId, userId);
        if (contributorIsAdmin)
        {
            contribution.Status = ContributionStatus.AwaitingVerification;
            await _context.SaveChangesAsync();

            var verifyError = await CreateVerificationAsync(
                round, contribution,
                initiatedByUserId: userId,        // contributor triggered it themselves
                excludeIds: new[] { userId, round.RecipientUserId });

            if (verifyError != null) return verifyError;
        }
        else
        {
            contribution.Status = ContributionStatus.Paid;
            await _context.SaveChangesAsync();
        }

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

        // ── Admin-pays guard ──────────────────────────────────────────────────
        if (userId == adminUserId)
            return "You cannot confirm your own contribution. It has been automatically sent for independent verification.";

        if (contribution.Status == ContributionStatus.AwaitingVerification)
            return "This contribution is already awaiting independent verification by a randomly assigned participant.";

        if (contribution.Status != ContributionStatus.Paid)
            return "Contribution must be in Paid status to confirm.";

        // Initiate two-step verification: set status to AwaitingVerification,
        // randomly pick a verifier (exclude both the contributor and the admin).
        contribution.Status = ContributionStatus.AwaitingVerification;
        await _context.SaveChangesAsync();

        var verifyError = await CreateVerificationAsync(
            round, contribution,
            initiatedByUserId: adminUserId,
            excludeIds: new[] { userId, adminUserId, round.RecipientUserId });

        if (verifyError != null)
        {
            // Roll back status so admin can retry
            contribution.Status = ContributionStatus.Paid;
            await _context.SaveChangesAsync();
            return verifyError;
        }

        var admin = await _context.Users.FindAsync(adminUserId);
        var member = await _context.Users.FindAsync(userId);
        await LogActivityAsync(roundId, adminUserId, RoundActivityAction.ContributionVerificationRequested,
            $"{admin?.FirstName} initiated verification of {member?.FirstName}'s contribution of {contribution.Amount:F2}");

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

        // ── Admin-self-payout guard ───────────────────────────────────────────
        if (round.RecipientUserId == adminUserId)
            return "You cannot record a payout to yourself. A randomly assigned independent verifier will complete this step.";

        // Ensure all contributions are confirmed or resolved before payout
        var unresolvedCount = await _context.MukandoContributions
            .CountAsync(c => c.MukandoRoundId == roundId
                          && (c.Status == ContributionStatus.Pending || c.Status == ContributionStatus.Paid
                              || c.Status == ContributionStatus.AwaitingVerification));
        if (unresolvedCount > 0)
            return $"{unresolvedCount} contribution(s) are still pending, unconfirmed, or awaiting verification. Resolve them first.";

        // Payout must match what was actually collected
        if (request.AmountDisbursed != round.ActualCollected)
            return $"Payout amount ({request.AmountDisbursed:F2}) does not match the collected amount ({round.ActualCollected:F2}).";

        // ── Check if recipient is a GroupAdmin (auto-route to random verifier) ─
        bool recipientIsAdmin = await IsGroupAdminOfCycleAsync(round.ExpenseCycleId, round.RecipientUserId);

        // Store payout data in a verification request; don't finalise the round yet.
        var verificationRequest = new MukandoVerificationRequest
        {
            MukandoRoundId         = roundId,
            Target                 = VerificationTarget.Payout,
            PendingPayoutAmount    = request.AmountDisbursed,
            PendingPayoutMethod    = method,
            PendingPayoutProofUrl  = request.ProofUrl,
            PendingPayoutReference = request.Reference,
            InitiatedByUserId      = adminUserId,
            Status                 = VerificationStatus.Pending,
            ExpiresAt              = DateTime.UtcNow.AddHours(48),
            CreatedAt              = DateTime.UtcNow
        };

        // Exclude: recipient (they benefit), the admin who initiated, and — if recipient
        // is also a GroupAdmin — exclude other admins too so a random non-admin gets chosen
        var excludeIds = new List<int> { adminUserId, round.RecipientUserId };
        if (recipientIsAdmin)
        {
            var otherAdmins = await GetCycleAdminIdsAsync(round.ExpenseCycleId);
            excludeIds.AddRange(otherAdmins);
        }

        var (verifierId, selectError) = await SelectRandomVerifierAsync(roundId, excludeIds);
        if (selectError != null) return selectError;

        verificationRequest.AssignedToUserId = verifierId;
        _context.MukandoVerificationRequests.Add(verificationRequest);
        await _context.SaveChangesAsync();

        var cycle  = await _context.ExpenseCycles.FindAsync(round.ExpenseCycleId);
        var currency = cycle != null ? await _context.Currencies.FindAsync(cycle.CurrencyId) : null;
        var sym    = currency?.Symbol ?? "$";
        var recipient = await _context.Users.FindAsync(round.RecipientUserId);

        await LogActivityAsync(roundId, adminUserId, RoundActivityAction.PayoutVerificationRequested,
            $"Payout of {sym}{request.AmountDisbursed:F2} to {recipient?.FirstName} submitted for independent verification");

        await _push.SendToUserAsync(
            verifierId,
            NotificationType.MukandoVerificationRequested,
            $"Verify payout: Round {round.RoundNumber}",
            $"You have been randomly selected to verify a payout of {sym}{request.AmountDisbursed:F2} to {recipient?.FirstName} for Round {round.RoundNumber} of \"{cycle?.Name}\". Open the cycle to review and respond.",
            $"/cycles/{round.ExpenseCycleId}",
            round.ExpenseCycleId);

        return null;
    }

    // ── Verification Flow ─────────────────────────────────────────────────────

    public async Task<List<MukandoVerificationRequestDto>> GetPendingVerificationsAsync(int cycleId, int requestingUserId, bool isAdmin)
    {
        var roundIds = await _context.MukandoRounds
            .Where(r => r.ExpenseCycleId == cycleId)
            .Select(r => r.Id)
            .ToListAsync();

        var query = _context.MukandoVerificationRequests
            .Where(v => roundIds.Contains(v.MukandoRoundId) && v.Status == VerificationStatus.Pending);

        // Non-admins only see their own assigned verification
        if (!isAdmin)
            query = query.Where(v => v.AssignedToUserId == requestingUserId);

        var items = await query.OrderBy(v => v.CreatedAt).ToListAsync();

        var userIds = items.SelectMany(v => new[] { v.AssignedToUserId, v.InitiatedByUserId })
            .Concat(items.Where(v => v.MukandoContributionId.HasValue).Select(v => 0))
            .Distinct().ToList();

        var users = await _context.Users
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id);

        var contributionIds = items.Where(v => v.MukandoContributionId.HasValue)
            .Select(v => v.MukandoContributionId!.Value).ToList();
        var contributions = await _context.MukandoContributions
            .Where(c => contributionIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id);

        var contributionUserIds = contributions.Values.Select(c => c.UserId).Distinct().ToList();
        var contributionUsers = await _context.Users
            .Where(u => contributionUserIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id);

        return items.Select(v =>
        {
            users.TryGetValue(v.AssignedToUserId, out var assignee);
            MukandoContribution? contrib = null;
            User? contributor = null;
            if (v.MukandoContributionId.HasValue)
            {
                contributions.TryGetValue(v.MukandoContributionId.Value, out contrib);
                if (contrib != null) contributionUsers.TryGetValue(contrib.UserId, out contributor);
            }

            // Verifier name is hidden until they respond (privacy to prevent collusion)
            bool revealed = v.Status != VerificationStatus.Pending;
            return new MukandoVerificationRequestDto(
                v.Id, v.MukandoRoundId, v.Target.ToString(), v.Status.ToString(),
                revealed ? v.AssignedToUserId : 0,
                revealed ? (assignee != null ? $"{assignee.FirstName} {assignee.LastName}" : "Unknown") : "Pending",
                v.ExpiresAt, v.CreatedAt,
                v.MukandoContributionId,
                contributor != null ? $"{contributor.FirstName} {contributor.LastName}" : null,
                contrib?.Amount,
                v.RejectionReason);
        }).ToList();
    }

    /// <summary>Called by the assigned verifier to approve or reject a contribution verification.</summary>
    public async Task<string?> VerifyContributionAsync(int verificationId, int verifierUserId, bool approve, string? rejectionReason)
    {
        var verification = await _context.MukandoVerificationRequests.FindAsync(verificationId);
        if (verification == null) return "Verification request not found.";
        if (verification.Target != VerificationTarget.Contribution) return "This is not a contribution verification.";
        if (verification.Status != VerificationStatus.Pending) return "This verification has already been responded to.";
        if (verification.AssignedToUserId != verifierUserId) return "You are not the assigned verifier for this request.";
        if (verification.ExpiresAt <= DateTime.UtcNow) return "This verification has expired. An admin can reassign it.";

        var round = await _context.MukandoRounds.FindAsync(verification.MukandoRoundId);
        if (round == null) return "Round not found.";

        var contribution = await _context.MukandoContributions.FindAsync(verification.MukandoContributionId);
        if (contribution == null) return "Contribution not found.";

        verification.RespondedByUserId = verifierUserId;
        verification.RespondedAt       = DateTime.UtcNow;

        if (approve)
        {
            contribution.Status             = ContributionStatus.Confirmed;
            contribution.ConfirmedByAdminAt = DateTime.UtcNow;
            round.ActualCollected          += contribution.Amount;
            round.UpdatedAt                 = DateTime.UtcNow;
            verification.Status             = VerificationStatus.Approved;

            await _context.SaveChangesAsync();

            var verifier = await _context.Users.FindAsync(verifierUserId);
            var member   = await _context.Users.FindAsync(contribution.UserId);
            await LogActivityAsync(round.Id, verifierUserId, RoundActivityAction.ContributionVerificationApproved,
                $"{verifier?.FirstName} independently verified and approved {member?.FirstName}'s contribution of {contribution.Amount:F2}");

            var cycle    = await _context.ExpenseCycles.FindAsync(round.ExpenseCycleId);
            var currency = cycle != null ? await _context.Currencies.FindAsync(cycle.CurrencyId) : null;
            var sym      = currency?.Symbol ?? "$";

            // Notify contributor
            await _push.SendToUserAsync(
                contribution.UserId,
                NotificationType.MukandoContributionConfirmed,
                $"Contribution confirmed: Round {round.RoundNumber}",
                $"Your contribution for Round {round.RoundNumber} has been independently verified and confirmed.",
                $"/cycles/{round.ExpenseCycleId}",
                round.ExpenseCycleId);

            // Notify admins + recipient if all contributions now resolved
            var allResolved = !await _context.MukandoContributions
                .AnyAsync(c => c.MukandoRoundId == round.Id
                    && c.Status != ContributionStatus.Confirmed
                    && c.Status != ContributionStatus.Missed);
            if (allResolved)
            {
                var adminIds = await GetCycleAdminIdsAsync(round.ExpenseCycleId);
                await _push.SendToUsersAsync(
                    adminIds,
                    NotificationType.MukandoAllContributionsCollected,
                    $"All contributions collected: Round {round.RoundNumber}",
                    $"All contributions for Round {round.RoundNumber} have been collected. {sym}{round.ActualCollected:F2} ready for payout.",
                    $"/cycles/{round.ExpenseCycleId}",
                    round.ExpenseCycleId);
            }
        }
        else
        {
            if (string.IsNullOrWhiteSpace(rejectionReason))
                return "A rejection reason is required.";

            contribution.Status    = ContributionStatus.Paid; // revert so admin can re-examine
            verification.Status    = VerificationStatus.Rejected;
            verification.RejectionReason = rejectionReason;

            await _context.SaveChangesAsync();

            var verifier = await _context.Users.FindAsync(verifierUserId);
            var member   = await _context.Users.FindAsync(contribution.UserId);
            await LogActivityAsync(round.Id, verifierUserId, RoundActivityAction.ContributionVerificationRejected,
                $"{verifier?.FirstName} rejected {member?.FirstName}'s contribution — reason: {rejectionReason}");

            var adminIds = await GetCycleAdminIdsAsync(round.ExpenseCycleId);
            await _push.SendToUsersAsync(
                adminIds,
                NotificationType.MukandoVerificationRejected,
                $"Contribution flagged: Round {round.RoundNumber}",
                $"An independent verifier flagged {member?.FirstName}'s contribution for Round {round.RoundNumber}. Reason: {rejectionReason}",
                $"/cycles/{round.ExpenseCycleId}",
                round.ExpenseCycleId);
        }

        return null;
    }

    /// <summary>Called by the assigned verifier to approve or reject a payout verification.</summary>
    public async Task<string?> VerifyPayoutAsync(int verificationId, int verifierUserId, bool approve, string? rejectionReason)
    {
        var verification = await _context.MukandoVerificationRequests.FindAsync(verificationId);
        if (verification == null) return "Verification request not found.";
        if (verification.Target != VerificationTarget.Payout) return "This is not a payout verification.";
        if (verification.Status != VerificationStatus.Pending) return "This verification has already been responded to.";
        if (verification.AssignedToUserId != verifierUserId) return "You are not the assigned verifier for this request.";
        if (verification.ExpiresAt <= DateTime.UtcNow) return "This verification has expired. An admin can reassign it.";

        var round = await _context.MukandoRounds.FindAsync(verification.MukandoRoundId);
        if (round == null) return "Round not found.";

        verification.RespondedByUserId = verifierUserId;
        verification.RespondedAt       = DateTime.UtcNow;

        if (approve)
        {
            verification.Status = VerificationStatus.Approved;

            var payout = new MukandoPayout
            {
                MukandoRoundId    = round.Id,
                RecipientUserId   = round.RecipientUserId,
                AmountDisbursed   = verification.PendingPayoutAmount!.Value,
                PaymentMethod     = verification.PendingPayoutMethod!.Value,
                ProofUrl          = verification.PendingPayoutProofUrl!,
                Reference         = verification.PendingPayoutReference,
                ConfirmedByUserId = verification.InitiatedByUserId, // original admin gets credit
                CreatedAt         = DateTime.UtcNow
            };

            _context.MukandoPayouts.Add(payout);

            round.PayoutConfirmed   = true;
            round.PayoutConfirmedAt = DateTime.UtcNow;
            round.Status            = RoundStatus.Completed;
            round.UpdatedAt         = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            var verifier  = await _context.Users.FindAsync(verifierUserId);
            var cycle     = await _context.ExpenseCycles.FindAsync(round.ExpenseCycleId);
            var currency  = cycle != null ? await _context.Currencies.FindAsync(cycle.CurrencyId) : null;
            var sym       = currency?.Symbol ?? "$";

            await LogActivityAsync(round.Id, verifierUserId, RoundActivityAction.PayoutVerificationApproved,
                $"{verifier?.FirstName} independently verified and approved payout of {sym}{payout.AmountDisbursed:F2}");

            await LogActivityAsync(round.Id, verification.InitiatedByUserId, RoundActivityAction.PayoutRecorded,
                $"Payout of {sym}{payout.AmountDisbursed:F2} finalised after independent verification");

            await _push.SendToUserAsync(
                round.RecipientUserId,
                NotificationType.MukandoPayoutConfirmed,
                $"Payout confirmed: Round {round.RoundNumber}",
                $"Your payout of {sym}{payout.AmountDisbursed:F2} for Round {round.RoundNumber} has been independently verified and confirmed!",
                $"/cycles/{round.ExpenseCycleId}",
                round.ExpenseCycleId);

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

            await ActivateNextRoundOrCompleteCycleAsync(round.ExpenseCycleId, verification.InitiatedByUserId);
        }
        else
        {
            if (string.IsNullOrWhiteSpace(rejectionReason))
                return "A rejection reason is required.";

            verification.Status          = VerificationStatus.Rejected;
            verification.RejectionReason = rejectionReason;

            await _context.SaveChangesAsync();

            var verifier  = await _context.Users.FindAsync(verifierUserId);
            var recipient = await _context.Users.FindAsync(round.RecipientUserId);
            var cycle     = await _context.ExpenseCycles.FindAsync(round.ExpenseCycleId);
            var currency  = cycle != null ? await _context.Currencies.FindAsync(cycle.CurrencyId) : null;
            var sym       = currency?.Symbol ?? "$";

            await LogActivityAsync(round.Id, verifierUserId, RoundActivityAction.PayoutVerificationRejected,
                $"{verifier?.FirstName} rejected payout of {sym}{verification.PendingPayoutAmount:F2} to {recipient?.FirstName} — reason: {rejectionReason}");

            var adminIds = await GetCycleAdminIdsAsync(round.ExpenseCycleId);
            await _push.SendToUsersAsync(
                adminIds,
                NotificationType.MukandoVerificationRejected,
                $"Payout flagged: Round {round.RoundNumber}",
                $"An independent verifier flagged the payout of {sym}{verification.PendingPayoutAmount:F2} to {recipient?.FirstName} for Round {round.RoundNumber}. Reason: {rejectionReason}. Please review and resubmit.",
                $"/cycles/{round.ExpenseCycleId}",
                round.ExpenseCycleId);
        }

        return null;
    }

    /// <summary>Admin reassigns an expired or stuck verification to a new random participant.</summary>
    public async Task<string?> ReassignVerifierAsync(int verificationId, int adminUserId)
    {
        var old = await _context.MukandoVerificationRequests.FindAsync(verificationId);
        if (old == null) return "Verification request not found.";
        if (old.Status != VerificationStatus.Pending) return "Only pending verifications can be reassigned.";

        var round = await _context.MukandoRounds.FindAsync(old.MukandoRoundId);
        if (round == null) return "Round not found.";

        // Mark old as reassigned
        old.Status = VerificationStatus.Reassigned;
        await _context.SaveChangesAsync();

        // Select a new verifier, excluding previous assignee as well
        var excludeIds = new List<int> { old.InitiatedByUserId, round.RecipientUserId, old.AssignedToUserId };
        if (old.MukandoContributionId.HasValue)
        {
            var contrib = await _context.MukandoContributions.FindAsync(old.MukandoContributionId.Value);
            if (contrib != null) excludeIds.Add(contrib.UserId);
        }

        var (newVerifierId, selectError) = await SelectRandomVerifierAsync(old.MukandoRoundId, excludeIds);
        if (selectError != null) return selectError;

        var newVerification = new MukandoVerificationRequest
        {
            MukandoRoundId             = old.MukandoRoundId,
            Target                     = old.Target,
            MukandoContributionId      = old.MukandoContributionId,
            PendingPayoutAmount        = old.PendingPayoutAmount,
            PendingPayoutMethod        = old.PendingPayoutMethod,
            PendingPayoutProofUrl      = old.PendingPayoutProofUrl,
            PendingPayoutReference     = old.PendingPayoutReference,
            InitiatedByUserId          = old.InitiatedByUserId,
            AssignedToUserId           = newVerifierId,
            Status                     = VerificationStatus.Pending,
            ExpiresAt                  = DateTime.UtcNow.AddHours(48),
            CreatedAt                  = DateTime.UtcNow
        };

        _context.MukandoVerificationRequests.Add(newVerification);
        await _context.SaveChangesAsync();

        await LogActivityAsync(old.MukandoRoundId, adminUserId, RoundActivityAction.VerificationReassigned,
            $"Verification reassigned to a new independent participant by admin");

        // Notify old assignee
        await _push.SendToUserAsync(
            old.AssignedToUserId,
            NotificationType.MukandoVerifierReassigned,
            "Verification reassigned",
            "Your verification assignment has been reassigned to another participant.",
            $"/cycles/{round.ExpenseCycleId}",
            round.ExpenseCycleId);

        // Notify new assignee
        var cycle = await _context.ExpenseCycles.FindAsync(round.ExpenseCycleId);
        await _push.SendToUserAsync(
            newVerifierId,
            NotificationType.MukandoVerificationRequested,
            $"Verify {old.Target.ToString().ToLower()}: Round {round.RoundNumber}",
            $"You have been randomly selected to verify a {old.Target.ToString().ToLower()} for Round {round.RoundNumber} of \"{cycle?.Name}\". Open the cycle to review and respond.",
            $"/cycles/{round.ExpenseCycleId}",
            round.ExpenseCycleId);

        return null;
    }

    // ── Verification Helpers ──────────────────────────────────────────────────

    /// <summary>
    /// Creates a verification request for a contribution and assigns a random verifier.
    /// </summary>
    private async Task<string?> CreateVerificationAsync(
        MukandoRound round,
        MukandoContribution contribution,
        int initiatedByUserId,
        IEnumerable<int> excludeIds)
    {
        var (verifierId, selectError) = await SelectRandomVerifierAsync(round.Id, excludeIds);
        if (selectError != null) return selectError;

        var verification = new MukandoVerificationRequest
        {
            MukandoRoundId        = round.Id,
            Target                = VerificationTarget.Contribution,
            MukandoContributionId = contribution.Id,
            InitiatedByUserId     = initiatedByUserId,
            AssignedToUserId      = verifierId,
            Status                = VerificationStatus.Pending,
            ExpiresAt             = DateTime.UtcNow.AddHours(48),
            CreatedAt             = DateTime.UtcNow
        };

        _context.MukandoVerificationRequests.Add(verification);
        await _context.SaveChangesAsync();

        var cycle    = await _context.ExpenseCycles.FindAsync(round.ExpenseCycleId);
        var currency = cycle != null ? await _context.Currencies.FindAsync(cycle.CurrencyId) : null;
        var sym      = currency?.Symbol ?? "$";
        var member   = await _context.Users.FindAsync(contribution.UserId);

        await _push.SendToUserAsync(
            verifierId,
            NotificationType.MukandoVerificationRequested,
            $"Verify contribution: Round {round.RoundNumber}",
            $"You have been randomly selected to verify {member?.FirstName}'s contribution of {sym}{contribution.Amount:F2} for Round {round.RoundNumber} of \"{cycle?.Name}\". Open the cycle to review and respond.",
            $"/cycles/{round.ExpenseCycleId}",
            round.ExpenseCycleId);

        await LogActivityAsync(round.Id, initiatedByUserId, RoundActivityAction.ContributionVerificationRequested,
            $"Independent verification requested for {member?.FirstName}'s contribution of {sym}{contribution.Amount:F2}");

        return null;
    }

    /// <summary>
    /// Picks a cryptographically random cycle participant, excluding specified user IDs.
    /// </summary>
    private async Task<(int verifierId, string? error)> SelectRandomVerifierAsync(
        int roundId, IEnumerable<int> excludeIds)
    {
        var round = await _context.MukandoRounds.FindAsync(roundId);
        if (round == null) return (0, "Round not found.");

        var excludeSet = new HashSet<int>(excludeIds);

        var candidates = await _context.CycleMembers
            .Where(m => m.ExpenseCycleId == round.ExpenseCycleId && !excludeSet.Contains(m.UserId))
            .Select(m => m.UserId)
            .ToListAsync();

        if (candidates.Count == 0)
            return (0, "No eligible independent verifier found. The cycle may not have enough participants.");

        // Cryptographically secure random selection
        int index = RandomNumberGenerator.GetInt32(candidates.Count);
        return (candidates[index], null);
    }

    /// <summary>Returns true if the given user is a GroupAdmin of the cycle's group.</summary>
    private async Task<bool> IsGroupAdminOfCycleAsync(int cycleId, int userId)
    {
        var groupId = await _context.ExpenseCycles
            .Where(c => c.Id == cycleId).Select(c => c.GroupId).FirstOrDefaultAsync();

        return await _context.GroupMembers
            .AnyAsync(gm => gm.GroupId == groupId
                         && gm.UserId == userId
                         && gm.GroupRole == GroupRole.GroupAdmin
                         && gm.Status == GroupInviteStatus.Accepted);
    }

    public async Task<string?> ForceCloseRoundAsync(int roundId, int adminUserId)    {
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

    private async Task ActivateNextRoundOrCompleteCycleAsync(int cycleId, int triggeredByUserId)
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

            await LogActivityAsync(nextRound.Id, triggeredByUserId, RoundActivityAction.RoundActivated,
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
