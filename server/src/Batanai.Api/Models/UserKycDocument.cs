using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Batanai.Api.Models;

/// <summary>
/// Stores identity verification documents submitted by a user for KYC review.
/// A user can have multiple records (each rejected submission is kept for audit).
/// Only the latest record drives User.KycStatus.
/// </summary>
public class UserKycDocument
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int UserId { get; set; }

    public KycIdType IdType { get; set; } = KycIdType.NationalId;

    /// <summary>The ID number printed on the document. Null when IdType = NoDocument.</summary>
    [StringLength(100)]
    public string? IdNumber { get; set; }

    /// <summary>Full name exactly as it appears on the identity document.</summary>
    [Required]
    [StringLength(200)]
    public string FullNameOnId { get; set; } = string.Empty;

    /// <summary>FK to UploadedFiles — scan/photo of the identity document. Null when IdType = NoDocument.</summary>
    public int? DocumentFileId { get; set; }

    /// <summary>FK to UploadedFiles — selfie of the person holding the document next to their face.</summary>
    public int? SelfieWithIdFileId { get; set; }

    public KycStatus Status { get; set; } = KycStatus.PendingReview;

    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Admin who reviewed this submission (approved or rejected via normal review).</summary>
    public int? ReviewedByUserId { get; set; }

    public DateTime? ReviewedAt { get; set; }

    /// <summary>Reason provided when the submission is rejected. Shown to the user so they can resubmit correctly.</summary>
    [StringLength(500)]
    public string? RejectionReason { get; set; }

    /// <summary>Admin note explaining why the normal document check was bypassed. Required when Status = AdminBypassed.</summary>
    [StringLength(500)]
    public string? AdminBypassNote { get; set; }

    // Navigation properties
    public User? User { get; set; }

    [ForeignKey(nameof(DocumentFileId))]
    public UploadedFile? DocumentFile { get; set; }

    [ForeignKey(nameof(SelfieWithIdFileId))]
    public UploadedFile? SelfieWithIdFile { get; set; }

    [ForeignKey(nameof(ReviewedByUserId))]
    public User? ReviewedBy { get; set; }
}
