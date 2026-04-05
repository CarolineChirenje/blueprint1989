using System.ComponentModel.DataAnnotations;

namespace Batanai.Api.Models;

public class Group
{
    [Key]
    public int Id { get; set; }

    [Required]
    [StringLength(150)]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }

    [Required]
    public bool IsActive { get; set; } = true;

    public int CreatedByUserId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [Required]
    [StringLength(8)]
    public string JoinCode { get; set; } = string.Empty;

    public DateTime JoinCodeGeneratedAt { get; set; } = DateTime.UtcNow;
}
