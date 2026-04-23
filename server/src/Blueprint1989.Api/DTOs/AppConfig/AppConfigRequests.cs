using System.ComponentModel.DataAnnotations;

namespace Blueprint1989.Api.DTOs.AppConfig;

public class UpdateAppConfigEntryRequest
{
    [Required]
    [StringLength(100)]
    public string Key { get; set; } = string.Empty;

    [Required]
    [StringLength(1000)]
    public string Value { get; set; } = string.Empty;
}

public class BulkUpdateAppConfigRequest
{
    [Required]
    [MinLength(1)]
    public List<UpdateAppConfigEntryRequest> Updates { get; set; } = new();
}
