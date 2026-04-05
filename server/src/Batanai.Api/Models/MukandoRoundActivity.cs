using System.ComponentModel.DataAnnotations;

namespace Batanai.Api.Models;

public class MukandoRoundActivity
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int MukandoRoundId { get; set; }

    [Required]
    public int UserId { get; set; }

    public RoundActivityAction Action { get; set; }

    [Required]
    [StringLength(500)]
    public string Details { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
