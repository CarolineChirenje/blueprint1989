using System.ComponentModel.DataAnnotations;

namespace Blueprint1989.Api.DTOs.Auth;

public class ChangeExpiredPasswordRequest
{
    [Required]
    public int UserId { get; set; }
    
    [Required]
    public string CurrentPassword { get; set; } = string.Empty;
    
    [Required]
    [StringLength(100, MinimumLength = 8)]
    [RegularExpression(@"^(?=.*[A-Z])(?=.*[a-z])(?=.*\d).{8,}$", 
        ErrorMessage = "Password must contain at least one uppercase letter, one lowercase letter, and one number.")]
    public string NewPassword { get; set; } = string.Empty;
}
