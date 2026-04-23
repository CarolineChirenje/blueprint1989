namespace Blueprint1989.Api.DTOs.AppConfig;

public class AppVersionDto
{
    public string FrontendVersion { get; set; } = string.Empty;
    public string BackendVersion { get; set; } = string.Empty;
}

public class AppConfigEntryDto
{
    public int Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string DataType { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsReadOnly { get; set; }
    public bool RequiresRestart { get; set; }
    public bool IsSecret { get; set; }
    public DateTime UpdatedAt { get; set; }
}
