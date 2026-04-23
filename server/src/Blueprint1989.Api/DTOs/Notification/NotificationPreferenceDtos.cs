namespace Blueprint1989.Api.DTOs.Notification;

/// <summary>Represents a single notification type with the user's current preference.</summary>
public class NotificationPreferenceDto
{
    public int TypeId { get; set; }
    public string TypeName { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>Whether push notifications of this type are enabled for the user.</summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// When true the user cannot change this setting themselves;
    /// only an Administrator or SuperAdmin can toggle it.
    /// </summary>
    public bool IsAdminControlled { get; set; }
}

/// <summary>A single preference item inside an update request.</summary>
public class UpdateNotificationPreferenceItem
{
    public int TypeId { get; set; }
    public bool IsEnabled { get; set; }
}

/// <summary>Request body for updating notification preferences.</summary>
public class UpdateNotificationPreferencesRequest
{
    public IList<UpdateNotificationPreferenceItem> Preferences { get; set; } = new List<UpdateNotificationPreferenceItem>();
}
