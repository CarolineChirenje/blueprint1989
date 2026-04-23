namespace Blueprint1989.Api.Models;

public class Notification
{
    public int Id { get; set; }

    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public string Message { get; set; } = string.Empty;

    public bool IsRead { get; set; } = false;

    public bool IsArchived { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Push notification enrichment fields
    public NotificationType Type { get; set; } = NotificationType.General;
    public string? DeepLinkUrl { get; set; }
    public int? RelatedEntityId { get; set; }
    public bool SentViaPush { get; set; } = false;
}
