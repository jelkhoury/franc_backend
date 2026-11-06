using FrancProject.Data;
using FrancProject.Models.ChatContent;
using FrancProject.Services.ChatContent;
using Microsoft.EntityFrameworkCore;
using Xunit;

public class ChatContentQueryTests
{
    private static DataContext Context() => new(new DbContextOptionsBuilder<DataContext>()
        .UseNpgsql("Host=localhost;Database=unused;Username=unused").Options);
    [Fact] public void SurveyVisibilityAndAssociationTranslateToPostgres()
    {
        using var db = Context(); var service = new ChatContentService(db);
        var query = service.BuildQuery<ChatSurvey>("survey", false, [1, 2], DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddDays(7), 5, "feedback", false).ToQueryString();
        Assert.Contains("published", query); Assert.Contains("ChatEvents", query); Assert.Contains("EventId", query);
        Assert.Contains("EXISTS", query); Assert.Contains("lower", query);
    }
    [Fact] public void EventDateRangeAndCancellationTranslateToPostgres()
    {
        using var db = Context(); var service = new ChatContentService(db);
        var query = service.BuildQuery<ChatEvent>("event", false, null, DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddDays(7), null, null, false).ToQueryString();
        Assert.Contains("StartsAt", query); Assert.Contains("EndsAt", query); Assert.Contains("IsCancelled", query);
    }
    [Fact] public void AdminQueryCanIncludeDrafts()
    {
        using var db = Context(); var service = new ChatContentService(db);
        var query = service.BuildQuery<ChatEvent>("event", true, null, null, null, null, null, true).ToQueryString();
        Assert.DoesNotContain("WHERE", query);
    }
    [Fact] public void BatchEventSurveyFilterTranslatesToPostgres()
    {
        using var db = Context(); var service = new ChatContentService(db);
        var query = service.BuildQuery<ChatSurvey>("survey", false, null, null, null, null, null, true, eventIds: [1, 2]).ToQueryString();
        Assert.Contains("COALESCE", query);
        Assert.Contains("ANY", query);
        Assert.Contains("published", query);
    }
}
