using System.ComponentModel.DataAnnotations;

namespace Batanai.Api.Models;

public class SentEmail
{
    [Key]
    public int Id { get; set; }

    [Required]
    [StringLength(255)]
    public string ToAddress { get; set; } = "";

    [Required]
    [StringLength(500)]
    public string Subject { get; set; } = "";

    [Required]
    public string HtmlBody { get; set; } = "";

    public string? PlainBody { get; set; }

    [StringLength(255)]
    public string? BccAddress { get; set; }

    public DateTime SentAt { get; set; } = DateTime.UtcNow;

    public bool IsSuccess { get; set; }

    public string? ErrorMessage { get; set; }
}
