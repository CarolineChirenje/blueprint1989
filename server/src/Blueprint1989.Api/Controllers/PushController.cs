using Blueprint1989.Api.Data;
using Blueprint1989.Api.DTOs.Notification;
using Blueprint1989.Api.Models;
using Blueprint1989.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Blueprint1989.Api.Controllers;

[ApiController]
[Route("api/push")]
public class PushController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IPushNotificationSender _pushSender;
    private readonly VapidSettings _vapid;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<PushController> _logger;
    private readonly IEmailService _emailService;

    public PushController(
        ApplicationDbContext context,
        IPushNotificationSender pushSender,
        IOptions<VapidSettings> vapidOptions,
        IWebHostEnvironment env,
        ILogger<PushController> logger,
        IEmailService emailService)
    {
        _context = context;
        _pushSender = pushSender;
        _emailService = emailService;
        _vapid = vapidOptions.Value;
        _env = env;
        _logger = logger;
    }

    // -- VAPID public key ----------------------------------------------------

    /// <summary>
    /// GET /api/push/vapid-public-key
    /// Returns the VAPID public key so the client can subscribe at runtime.
    /// Anonymous � the key is not sensitive.
    /// </summary>
    [HttpGet("vapid-public-key")]
    [AllowAnonymous]
    public IActionResult GetVapidPublicKey()
    {
        return Ok(new VapidPublicKeyResponse(_vapid.PublicKey));
    }

    // -- Subscription management ---------------------------------------------

    /// <summary>
    /// POST /api/push/subscribe
    /// Upserts a push subscription for the authenticated user.
    /// Called by the Angular app after PushManager.subscribe() succeeds.
    /// </summary>
    [HttpPost("subscribe")]
    [Authorize]
    public async Task<IActionResult> Subscribe([FromBody] PushSubscribeRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        // Reclaim endpoint: remove subscriptions from other users for the same
        // browser endpoint so that a shared device only delivers notifications
        // to the user who most recently logged in.
        await _context.PushSubscriptions
            .Where(s => s.Endpoint == request.Endpoint && s.UserId != userId)
            .ExecuteDeleteAsync();

        var existing = await _context.PushSubscriptions
            .FirstOrDefaultAsync(s => s.UserId == userId && s.Endpoint == request.Endpoint);

        if (existing != null)
        {
            // Update keys in case they rotated
            existing.P256dh = request.P256dh;
            existing.Auth = request.Auth;
            existing.UserAgent = request.UserAgent;
        }
        else
        {
            _context.PushSubscriptions.Add(new PushSubscription
            {
                UserId = userId.Value,
                Endpoint = request.Endpoint,
                P256dh = request.P256dh,
                Auth = request.Auth,
                UserAgent = request.UserAgent,
                CreatedAt = DateTime.UtcNow
            });
        }

        await _context.SaveChangesAsync();
        _logger.LogInformation("Push subscription registered for user {UserId}", userId);
        return NoContent();
    }

    /// <summary>
    /// DELETE /api/push/unsubscribe
    /// Removes the matching push subscription for the authenticated user.
    /// </summary>
    [HttpDelete("unsubscribe")]
    [Authorize]
    public async Task<IActionResult> Unsubscribe([FromBody] PushSubscribeRequest request)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        await _context.PushSubscriptions
            .Where(s => s.UserId == userId && s.Endpoint == request.Endpoint)
            .ExecuteDeleteAsync();

        return NoContent();
    }

    // -- Dev / Admin test endpoint --------------------------------------------

    /// <summary>
    /// POST /api/push/test
    /// Sends a test push notification to the authenticated user.
    /// In Development: accessible to all authenticated users.
    /// In Production: restricted to Admin and above.
    /// </summary>
    [HttpPost("test")]
    [Authorize]
    public async Task<IActionResult> SendTestPush([FromBody] TestPushRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        // Enforce role restriction in production
        if (!_env.IsDevelopment())
        {
            var role = User.FindFirst("role")?.Value;
            if (role is not ("SuperAdmin" or "Administrator"))
                return Forbid();
        }

        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        try
        {
            await _pushSender.SendToUserAsync(
                userId: userId.Value,
                type: request.Type,
                title: request.Title,
                body: request.Body,
                deepLinkUrl: request.DeepLinkUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SendTestPush failed for user {UserId}", userId);
            return StatusCode(500, new { message = "Push delivery failed. Check server logs for details." });
        }

        return Ok(new { message = "Test push dispatched." });
    }

    // -- Dev / Admin test email endpoint -------------------------------------

    /// <summary>
    /// POST /api/push/test-email
    /// Sends a test email to the specified address.
    /// In Development: accessible to all authenticated users.
    /// In Production: restricted to Admin and above.
    /// </summary>
    [HttpPost("test-email")]
    [Authorize]
    public async Task<IActionResult> SendTestEmail([FromBody] TestEmailRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        // Enforce role restriction in production
        if (!_env.IsDevelopment())
        {
            var role = User.FindFirst("role")?.Value;
            if (role is not ("SuperAdmin" or "Administrator"))
                return Forbid();
        }

        var sent = await _emailService.SendEmailAsync(
            to: request.To,
            subject: request.Subject,
            htmlBody: $"<p>{System.Web.HttpUtility.HtmlEncode(request.Body)}</p>",
            plainBody: request.Body);

        if (!sent)
            return BadRequest(new { message = "Email failed to send — check SMTP configuration." });

        _logger.LogInformation("Test email sent to {Recipient}", request.To);
        return Ok(new { message = $"Test email dispatched to {request.To}." });
    }

    // -- Helpers --------------------------------------------------------------

    private int? GetUserId()
    {
        var claim = User.FindFirst("id");
        return claim != null && int.TryParse(claim.Value, out var id) ? id : null;
    }
}

public record TestEmailRequest(
    [property: System.ComponentModel.DataAnnotations.Required]
    [property: System.ComponentModel.DataAnnotations.EmailAddress]
    string To,
    [property: System.ComponentModel.DataAnnotations.Required]
    string Subject,
    [property: System.ComponentModel.DataAnnotations.Required]
    string Body
);
