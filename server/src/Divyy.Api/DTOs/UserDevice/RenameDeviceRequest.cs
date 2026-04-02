using System.ComponentModel.DataAnnotations;

namespace Divvy.Api.DTOs.UserDevice;

public class RenameDeviceRequest
{
    [Required]
    [StringLength(100, MinimumLength = 1, ErrorMessage = "Friendly name must be between 1 and 100 characters.")]
    public string FriendlyName { get; set; } = null!;
}
