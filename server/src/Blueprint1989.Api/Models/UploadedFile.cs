using System.ComponentModel.DataAnnotations;

namespace Blueprint1989.Api.Models;

public class UploadedFile
{
    public int Id { get; set; }

    [Required, MaxLength(255)]
    public string FileName { get; set; } = null!;

    [Required, MaxLength(100)]
    public string ContentType { get; set; } = null!;

    [Required]
    public byte[] Data { get; set; } = null!;

    public long SizeBytes { get; set; }

    [Required, MaxLength(50)]
    public string Folder { get; set; } = "proofs";

    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    public int UploadedByUserId { get; set; }
    public User? UploadedBy { get; set; }
}
