using FrancProject.Controllers;
using FrancProject.Data;
using FrancProject.Models.ChatContent;
using FrancProject.Services.ChatContent;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

public class ChatContentAvailabilityTests
{
    private static DataContext Context() => new(new DbContextOptionsBuilder<DataContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    [Theory]
    [InlineData("scheduled", 1)]
    [InlineData("ongoing", 1)]
    [InlineData("started", 1)]
    [InlineData("ended", 1)]
    [InlineData("cancelled", 1)]
    [InlineData("available", 2)]
    [InlineData("all", 5)]
    public async Task EventFilterMatchesAvailabilityAndExcludesUnpublishedRecords(string availability, int count)
    {
        using var db = Context();
        var now = DateTimeOffset.UtcNow;
        db.ChatEvents.AddRange(
            Event("Scheduled", now.AddDays(1), null),
            Event("Ongoing", now.AddDays(-1), now.AddDays(1)),
            Event("Started", now.AddDays(-1), null),
            Event("Ended", now.AddDays(-2), now.AddDays(-1)),
            Event("Cancelled", now.AddDays(1), null, cancelled: true),
            Event("Draft", now.AddDays(1), null, status: "draft"),
            Event("Archived", now.AddDays(-2), now.AddDays(-1), status: "archived"));
        await db.SaveChangesAsync();
        var result = await new ChatContentService(db).List("event", false, null, null, null, null, null,
            false, 1, 20, default, availability: availability);
        Assert.Equal(count, result.Total);
        Assert.Equal(count, result.Items.Count);
        Assert.All(result.Items, x => Assert.Equal("published", x.Status));
        if (availability is not ("available" or "all"))
            Assert.All(result.Items, x => Assert.Equal(availability, x.Availability));
    }

    [Theory]
    [InlineData("open", 1)]
    [InlineData("closed", 2)]
    [InlineData("not_open", 1)]
    [InlineData("cancelled", 1)]
    [InlineData("available", 1)]
    [InlineData("all", 5)]
    public async Task SurveyFilterAppliesBeforePaginationAndRetainsParentVisibility(string availability, int count)
    {
        using var db = Context();
        var now = DateTimeOffset.UtcNow;
        var draft = Event("Hidden parent", now.AddDays(1), null, status: "draft");
        db.ChatEvents.Add(draft);
        db.ChatSurveys.AddRange(
            Survey("Open", null, null),
            Survey("Closed 1", null, now.AddDays(-1)),
            Survey("Closed 2", null, now.AddDays(-2)),
            Survey("Future", now.AddDays(1), null),
            Survey("Cancelled", null, null, cancelled: true),
            new ChatSurvey {Name = "Hidden", Description = "Hidden", Status = "published", Event = draft},
            new ChatSurvey {Name = "Draft", Description = "Draft", Status = "draft"});
        await db.SaveChangesAsync();
        var service = new ChatContentService(db);
        var result = await service.List("survey", false, null, null, null, null, null,
            false, 1, 1, default, availability: availability);
        Assert.Equal(count, result.Total);
        Assert.Single(result.Items);
        Assert.Equal("published", result.Items[0].Status);
        Assert.Null(result.Items[0].EventId);
        if (availability is not ("available" or "all")) Assert.Equal(availability, result.Items[0].Availability);
        if (availability == "closed") {
            var second = await service.List("survey", false, null, null, null, null, null,
                false, 2, 1, default, availability: availability);
            Assert.Single(second.Items);
            Assert.NotEqual(result.Items[0].Id, second.Items[0].Id);
        }
    }

    [Fact]
    public async Task PastDateListingWithAllFilterReturnsEndedEvent()
    {
        using var db = Context();
        var now = DateTimeOffset.UtcNow;
        db.ChatEvents.Add(Event("Last month", now.AddDays(-30), now.AddDays(-29)));
        await db.SaveChangesAsync();
        var result = await new ChatContentService(db).List("event", false, null, now.AddDays(-31),
            now.AddDays(-28), null, null, false, 1, 20, default, availability: "all");
        Assert.Single(result.Items);
        Assert.Equal("ended", result.Items[0].Availability);
    }

    [Theory]
    [InlineData("event", "closed")]
    [InlineData("survey", "ongoing")]
    [InlineData("event", "invalid")]
    public async Task ApiRejectsAvailabilityThatDoesNotApplyToType(string type, string availability)
    {
        using var db = Context();
        var controller = new ChatContentController(new ChatContentService(db));
        var response = await controller.List(type, null, null, null, null, null, null, availability: availability);
        Assert.IsType<BadRequestObjectResult>(response);
    }

    private static ChatEvent Event(string name, DateTimeOffset start, DateTimeOffset? end,
        bool cancelled = false, string status = "published") => new() {
        Name = name, Description = name, StartsAt = start, EndsAt = end, Status = status, IsCancelled = cancelled
    };
    private static ChatSurvey Survey(string name, DateTimeOffset? start, DateTimeOffset? end, bool cancelled = false) => new() {
        Name = name, Description = name, StartsAt = start, EndsAt = end, Status = "published", IsCancelled = cancelled
    };
}
