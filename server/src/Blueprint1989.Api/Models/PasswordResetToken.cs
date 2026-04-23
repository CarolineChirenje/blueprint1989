using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Blueprint1989.Api.Models;

/// <summary>
/// Tracks password reset tokens for the forgot password feature.
/// Each token is single-use and expires after a configured duration.
/// </summary>
public class PasswordResetToken
{
    [Key]
    public int Id { get; set; }

    [Required]
    [ForeignKey(nameof(User))]
    public int UserId { get; set; }

    [Required]
    [StringLength(255)]
    public string Token { get; set; } = string.Empty;  // PBKDF2 hashed token

    [Required]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Required]
    public DateTime ExpiresAt { get; set; }

    [Required]
    public bool IsUsed { get; set; } = false;

    public DateTime? UsedAt { get; set; }

    // Navigation
    public User? User { get; set; }
}
