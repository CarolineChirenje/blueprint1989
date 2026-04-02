using Divvy.Api.Models;

namespace Divvy.Api.DTOs.Notification;

public record NotificationDto(
    int Id,
    string Message,
    bool IsRead,
    DateTime CreatedAt,
    NotificationType Type,
    string? DeepLinkUrl,
    int? RelatedEntityId,
    bool SentViaPush,
    bool IsArchived
);

public record NotificationSummaryDto(
    int UnreadCount,
    List<NotificationDto> Notifications
);

public record MarkNotificationsReadRequest(
    List<int> NotificationIds
);

// Subscription management DTOs
public record PushSubscribeRequest(
    string Endpoint,
    string P256dh,
    string Auth,
    string? UserAgent
);

public record VapidPublicKeyResponse(string PublicKey);

// BG timer DTOs
public record ScheduleBgTimerRequest(int CareRecipientId, int DelayMinutes = 120);

// Dev test DTO — uses init properties (not positional constructor) so System.Text.Json
// can deserialize the NotificationType enum from an integer value sent by the client.
public class TestPushRequest
{
    public NotificationType Type { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Body { get; init; } = string.Empty;
    public string? DeepLinkUrl { get; init; }
}
