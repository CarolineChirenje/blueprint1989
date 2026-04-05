using System.ComponentModel.DataAnnotations;

namespace Batanai.Api.Models;

/// <summary>
/// Lookup table for the NotificationType enum.
/// The Id matches the enum's integer value (starts at 1).
/// </summary>
public class NotificationTypeEntity
{
    /// <summary>Primary key — matches the integer value of the <see cref="NotificationType"/> enum.</summary>
    [Key]
    public int Id { get; set; }

    [Required]
    [StringLength(50)]
    public string Name { get; set; } = string.Empty;

    [StringLength(200)]
    public string? Description { get; set; }

    /// <summary>
    /// When true, only admins can toggle this type for a user.
    /// The user sees it as read-only (always enabled).
    /// </summary>
    public bool IsAdminControlled { get; set; } = false;
}
