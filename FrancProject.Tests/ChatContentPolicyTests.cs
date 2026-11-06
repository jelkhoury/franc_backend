using FrancProject.Dto.ChatContent;
using FrancProject.Services.ChatContent;
using Xunit;

public class ChatContentPolicyTests
{
    private static ChatContentInput Event() => new() { Name = "Workshop", Description = "Robotics", StartsAt = DateTimeOffset.Parse("2026-11-10T10:00:00Z"), EndsAt = DateTimeOffset.Parse("2026-11-10T12:00:00Z"), TimeZone = "Asia/Beirut" };
    [Fact] public void RejectsUnsafeSurveyUrl() { var input = Event(); input.Url = "javascript:alert(1)"; Assert.NotEmpty(ChatContentPolicy.Validate("survey", input)); }
    [Fact] public void RejectsReversedEventDates() { var input = Event(); input.EndsAt = input.StartsAt!.Value.AddHours(-1); Assert.NotEmpty(ChatContentPolicy.Validate("event", input)); }
    [Fact] public void AcceptsValidEvent() => Assert.Empty(ChatContentPolicy.Validate("event", Event()));
    [Fact] public void SurveyRequiresUrl() => Assert.NotEmpty(ChatContentPolicy.Validate("survey", new() { Name = "Feedback", Description = "Tell us" }));
    [Fact] public void RejectsUnknownTimeZone() { var input = Event(); input.TimeZone = "invalid/zone"; Assert.NotEmpty(ChatContentPolicy.Validate("event", input)); }
    [Fact] public void ClosedSurveyHasNoActionLink() { var now = DateTimeOffset.UtcNow; Assert.Equal("closed", ChatContentPolicy.Availability("survey", false, null, now.AddMinutes(-1), now)); }
    [Fact] public void CancelledEventHasCancelledStatus() => Assert.Equal("cancelled", ChatContentPolicy.Availability("event", true, null, null, DateTimeOffset.UtcNow));
    [Fact] public void PastEventWithoutEndIsNotScheduled() => Assert.Equal("started", ChatContentPolicy.Availability("event", false, DateTimeOffset.UtcNow.AddDays(-1), null, DateTimeOffset.UtcNow));
}
