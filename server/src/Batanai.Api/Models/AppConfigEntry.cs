using System.ComponentModel.DataAnnotations;

namespace Batanai.Api.Models;

public class AppConfigEntry
{
    [Key]
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
    public string Key { get; set; } = string.Empty;

    [Required]
    [StringLength(1000)]
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// Primitive type hint for UI rendering and validation.
    /// Accepted values: "int", "decimal", "bool", "string", "json"
    /// </summary>
    [Required]
    [StringLength(20)]
    public string DataType { get; set; } = "string";

    /// <summary>
    /// Groups related settings in the admin UI.
    /// e.g. "System", "Authentication", "Assessment", "BloodPressure", "Notifications"
    /// </summary>
    [Required]
    [StringLength(50)]
    public string Category { get; set; } = string.Empty;

    [Required]
    [StringLength(200)]
    public string DisplayName { get; set; } = string.Empty;

    [StringLength(500)]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// When true the value is masked in the admin UI and treated as a secret.
    /// </summary>
    public bool IsSecret { get; set; } = false;

    /// <summary>
    /// When true the value is displayed but cannot be edited through the admin UI.
    /// Used for system-managed values such as version numbers.
    /// </summary>
    public bool IsReadOnly { get; set; } = false;

    /// <summary>
    /// When true a warning is shown in the UI stating a service restart is required
    /// for the change to take effect.
    /// </summary>
    public bool RequiresRestart { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
