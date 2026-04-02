using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace Divvy.Api.Models;

public enum Role
{
    SuperAdmin = 1,
    Admin      = 2,
    Member     = 3
}

public class User
{
    [Key]
    public int Id { get; set; }
    
    [Required]
    [StringLength(255)]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;
    
    [Required]
    [StringLength(100)]
    public string FirstName { get; set; } = string.Empty;
    
    [Required]
    [StringLength(100)]
    public string LastName { get; set; } = string.Empty;
    
    [Required]
    [StringLength(255)]
    public string PasswordHash { get; set; } = string.Empty;
    
    public DateTime? PasswordLastChanged { get; set; }
    
    [Required]
    public Role Role { get; set; } = Role.Member;
    
    [Required]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    [Required]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    [Required]
    public bool IsActive { get; set; } = true;
    
    // MFA Support
    public bool IsMfaEnabled { get; set; } = false;
    
    [StringLength(255)]
    public string? MfaSecret { get; set; } // Base32 encoded TOTP secret
    
    public DateTime? MfaEnabledAt { get; set; }

    public string BackupCodesJson { get; set; } = "[]";

    /// <summary>Whether the user has confirmed their email address via the verification link sent on signup.</summary>
    [Required]
    public bool IsEmailVerified { get; set; } = false;

    [NotMapped]
    public List<string> BackupCodes
    { 
        get => string.IsNullOrEmpty(BackupCodesJson) 
            ? new List<string>() 
            : JsonSerializer.Deserialize<List<string>>(BackupCodesJson) ?? new List<string>();
        set => BackupCodesJson = JsonSerializer.Serialize(value);
    }
}
