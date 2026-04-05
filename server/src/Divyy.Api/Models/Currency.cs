using System.ComponentModel.DataAnnotations;

namespace Divvy.Api.Models;

public class Currency
{
    [Key]
    public int Id { get; set; }

    [Required]
    [StringLength(3)]
    public string Code { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [StringLength(5)]
    public string Symbol { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
}
