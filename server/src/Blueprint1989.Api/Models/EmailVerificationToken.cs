using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Blueprint1989.Api.Models;

/// <summary>
/// Tracks email verification tokens sent on signup.
/// Each token is single-use and expires after a configured duration.
/// </summary>
public class EmailVerificationToken
{
    [Key]
    public int Id { get; set; }

    [Required]
    [ForeignKey(nameof(User))]
    public int UserId { get; set; }

    /// <summary>SHA-256 hex hash of the plain token. Never store the plain token.</summary>
    [Required]
    [StringLength(255)]
    public string Token { get; set; } = string.Empty;

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
