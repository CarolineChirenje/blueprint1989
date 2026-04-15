namespace Batanai.Api.Services.Reminders;

public record ReminderMilestone(
    string StageKey,
    DateTime ScheduledForUtc,
    Models.NotificationType NotificationType,
    string TitleTemplate,
    string BodyTemplate);

public interface IReminderPolicy
{
    Task<List<ReminderMilestone>> CalculateMilestonesAsync(DateTime startUtc, DateTime endUtc);
}
