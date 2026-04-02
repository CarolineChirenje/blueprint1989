using System.ComponentModel.DataAnnotations;

namespace Divvy.Api.DTOs.Auth;

public class MfaVerifyRequest
{
    [Required]
    [Range(1, int.MaxValue)]
    public int UserId { get; set; }
    
    [Required]
    [StringLength(10, MinimumLength = 6)]
    public string Code { get; set; } = string.Empty;
}

public class MfaSetupResponse
{
    public string QrCode { get; set; } = string.Empty;
    public string Secret { get; set; } = string.Empty;
    public List<string> BackupCodes { get; set; } = new List<string>();
}

public class ConfirmMfaRequest
{
    [Required]
    public string Secret { get; set; } = string.Empty;
    
    [Required]
    [StringLength(10, MinimumLength = 6)]
    public string VerificationCode { get; set; } = string.Empty;
    
    [Required]
    public List<string> BackupCodes { get; set; } = new List<string>();
}

public class SkipMfaSetupRequest
{
    [Required]
    [Range(1, int.MaxValue)]
    public int UserId { get; set; }

    [Required]
    public string SkipToken { get; set; } = string.Empty;
}
