using System.ComponentModel.DataAnnotations;
using Batanai.Api.Models;

namespace Batanai.Api.DTOs.Auth;

public class SignupRequest
{
    [Required]
    [EmailAddress]
    [StringLength(255)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string LastName { get; set; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 8)]
    public string Password { get; set; } = string.Empty;

    [Required]
    public Role Role { get; set; }

    public string? AdminPin { get; set; }
}

public class LoginRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}

public class RegisterRequest
{
    [Required]
    [EmailAddress]
    [StringLength(255)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 8)]
    public string Password { get; set; } = string.Empty;

    [Required]
    public Role Role { get; set; } = Role.Member;
}

public class AuthResponse
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public Role Role { get; set; } = Role.Member;
    public string Token { get; set; } = string.Empty;
    public DateTime? TokenExpiresAt { get; set; }
    public bool IsMfaRequired { get; set; } = false;
    public bool IsMfaEnabled { get; set; } = false;
    public DateTime? MfaEnabledAt { get; set; }
    public string? MfaTempToken { get; set; }
    public bool PasswordExpired { get; set; } = false;
}

public class SeenVersionRequest
{
    [Required]
    [StringLength(20)]
    public string Version { get; set; } = string.Empty;
}