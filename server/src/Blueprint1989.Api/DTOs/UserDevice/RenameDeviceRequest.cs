using System.ComponentModel.DataAnnotations;

namespace Blueprint1989.Api.DTOs.UserDevice;

public class RenameDeviceRequest
{
    [Required]
    [StringLength(100, MinimumLength = 1, ErrorMessage = "Friendly name must be between 1 and 100 characters.")]
    public string FriendlyName { get; set; } = null!;
}
