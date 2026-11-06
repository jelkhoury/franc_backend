using FrancProject.Data;
using FrancProject.Dto.ChatContent;
using FrancProject.Models.ChatContent;
using FrancProject.Services.ChatContent;
using Microsoft.EntityFrameworkCore;
using Xunit;

public class ChatContentServiceTests
{
    private static DataContext Context() => new(new DbContextOptionsBuilder<DataContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private static ChatContentInput Event(string status = "published") => new() {
        Name = "Robotics", Description = "University workshop", Status = status,
        StartsAt = DateTimeOffset.Parse("2026-12-10T10:00:00+02:00")
    };
    [Fact] public async Task SaveCreatesRecordAndDurableJobWithGeneratedForeignKey()
    {
        using var db = Context(); var service = new ChatContentService(db);
        var saved = await service.Save("event", null, Event(), default);
        var job = await db.ChatContentIndexJobs.SingleAsync();
        Assert.Equal(saved.Id, job.EventId); Assert.Null(job.SurveyId);
        Assert.Equal(saved.Id, job.RecordId); Assert.Equal(saved.Version, job.Version);
        Assert.Equal("pending", saved.EmbeddingStatus);
        Assert.Equal(TimeSpan.Zero, saved.StartsAt!.Value.Offset);
    }
    [Fact] public async Task EditsCreateVersionedJobsAndRejectStaleVersion()
    {
        using var db = Context(); var service = new ChatContentService(db);
        var saved = await service.Save("event", null, Event(), default);
        var update = Event(); update.Version = saved.Version; update.Name = "Robotics updated";
        var edited = await service.Save("event", saved.Id, update, default);
        Assert.Equal(2, edited.Version);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => service.Save("event", saved.Id, update, default));
        Assert.Equal(2, await db.ChatContentIndexJobs.CountAsync());
    }
    [Fact] public async Task PublishedSurveyForDraftEventIsHiddenFromUsers()
    {
        using var db = Context(); var service = new ChatContentService(db);
        var parent = await service.Save("event", null, Event("draft"), default);
        var survey = await service.Save("survey", null, new() { Name = "Feedback", Description = "Workshop feedback",
            Status = "published", Url = "https://example.org/form", EventId = parent.Id }, default);
        Assert.Null(await service.Find("survey", survey.Id, false, default));
        Assert.NotNull(await service.Find("survey", survey.Id, true, default));
    }
    [Fact] public async Task UnpublishingHidesPreviouslyPublishedRecordImmediately()
    {
        using var db = Context(); var service = new ChatContentService(db);
        var saved = await service.Save("event", null, Event(), default);
        var update = Event("archived"); update.Version = saved.Version;
        await service.Save("event", saved.Id, update, default);
        Assert.Null(await service.Find("event", saved.Id, false, default));
    }
    [Fact] public async Task SurveyAssociationQueryDoesNotReturnOtherEventsSurvey()
    {
        using var db = Context(); var service = new ChatContentService(db);
        var first = await service.Save("event", null, Event(), default);
        var second = await service.Save("event", null, Event(), default);
        foreach (var id in new[] {first.Id, second.Id}) await service.Save("survey", null,
            new() {Name = "Feedback", Description = "Feedback", Url = "https://example.org/form", Status = "published", EventId = id}, default);
        var result = await service.List("survey", false, null, null, null, first.Id, null, true, 1, 20, default);
        Assert.Single(result.Items); Assert.Equal(first.Id, result.Items[0].EventId);
    }
    [Fact] public async Task BatchRelatedSurveysExcludeOtherEventsAndDrafts()
    {
        using var db = Context(); var service = new ChatContentService(db);
        var first = await service.Save("event", null, Event(), default);
        var second = await service.Save("event", null, Event(), default);
        foreach (var status in new[] {"published", "draft"}) await service.Save("survey", null,
            new() {Name = "Feedback", Description = "Feedback description", Url = "https://example.org/form", Status = status, EventId = first.Id}, default);
        await service.Save("survey", null, new() {Name = "Other feedback", Description = "Other", Url = "https://example.org/other", Status = "published", EventId = second.Id}, default);
        var result = await service.List("survey", false, null, null, null, null, null, true, 1, 100, default, eventIds: [first.Id]);
        Assert.Single(result.Items);
        Assert.Equal(first.Id, result.Items[0].EventId);
    }
}
