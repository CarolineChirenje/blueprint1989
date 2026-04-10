using System.ComponentModel.DataAnnotations;
using Batanai.Api.Models;

namespace Batanai.Api.DTOs.Kyc;

public class SubmitKycRequest
{
    [Required]
    public KycIdType IdType { get; set; }

    /// <summary>Required unless IdType = NoDocument.</summary>
    [StringLength(100)]
    public string? IdNumber { get; set; }

    [Required]
    [StringLength(200)]
    public string FullNameOnId { get; set; } = string.Empty;

    /// <summary>File ID (from /api/files/upload) for the document photo. Optional for NoDocument.</summary>
    public int? DocumentFileId { get; set; }

    /// <summary>File ID for the selfie holding the document next to face. Optional but encouraged.</summary>
    public int? SelfieWithIdFileId { get; set; }
}

public class ReviewKycRequest
{
    [Required]
    public bool Approve { get; set; }

    /// <summary>Required when Approve = false.</summary>
    [StringLength(500)]
    public string? RejectionReason { get; set; }
}

public class BypassKycRequest
{
    [Required]
    [StringLength(500)]
    public string Note { get; set; } = string.Empty;
}

public class KycStatusDto
{
    public KycStatus Status { get; set; }
    public string? RejectionReason { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public bool CanResubmit { get; set; }
}

public class KycDocumentDto
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string UserEmail { get; set; } = string.Empty;
    public KycIdType IdType { get; set; }
    public string? IdNumber { get; set; }
    public string FullNameOnId { get; set; } = string.Empty;
    public string? DocumentFileUrl { get; set; }
    public string? SelfieWithIdFileUrl { get; set; }
    public KycStatus Status { get; set; }
    public DateTime SubmittedAt { get; set; }
    public string? RejectionReason { get; set; }
    public string? AdminBypassNote { get; set; }
}
