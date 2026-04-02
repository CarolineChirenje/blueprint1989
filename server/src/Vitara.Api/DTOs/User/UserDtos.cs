using System.ComponentModel.DataAnnotations;
using Divvy.Api.Models;

namespace Divvy.Api.DTOs.User;

public class UserManagementDto
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string FullName { get; set; } = string.Empty;
    public Role Role { get; set; } = Role.Member;
    public string RoleName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public string ActiveStatus { get; set; } = string.Empty;
    public bool IsMfaEnabled { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class UpdateUserStatusRequest
{
    [Required]
    [Range(1, int.MaxValue)]
    public int UserId { get; set; }

    [Required]
    public bool IsActive { get; set; }
}

public class UpdateUserRequest
{
    [Required]
    [Range(1, int.MaxValue)]
    public int UserId { get; set; }

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [StringLength(100, MinimumLength = 2)]
    public string? FirstName { get; set; }

    [StringLength(100, MinimumLength = 2)]
    public string? LastName { get; set; }

    [Required]
    public Role Role { get; set; }
}
