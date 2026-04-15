using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Batanai.Api.Models;

/// <summary>
/// Per-user opt-in/out setting for a specific notification type.
/// Rows are created on first explicit change; absence means the default (enabled).
/// </summary>
public class UserNotificationPreference
{
    [Required]
    public int UserId { get; set; }

    [Required]
    public int NotificationTypeId { get; set; }

    /// <summary>
    /// Whether the user wants to receive push notifications of this type.
    /// In-app bell notifications are always created regardless of this setting.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    // -- Navigation ----------------------------------------------------------

    [ForeignKey(nameof(UserId))]
    public User User { get; set; } = null!;

    [ForeignKey(nameof(NotificationTypeId))]
    public NotificationTypeEntity NotificationType { get; set; } = null!;
}
