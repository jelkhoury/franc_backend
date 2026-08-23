using FrancProject.Data;
using FrancProject.Interfaces;
using FrancProject.Models.Analytics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FrancProject.Services.Analytics;

public class ActivityEventWriter : IActivityEventWriter
{
    private readonly DataContext _context;
    private readonly ILogger<ActivityEventWriter> _logger;

    public ActivityEventWriter(DataContext context, ILogger<ActivityEventWriter> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task TryLogAsync(
        int userId,
        string serviceKey,
        string activityType,
        string status,
        string entityId,
        DateTime occurredAt,
        string? resultSummary = null)
    {
        try
        {
            var exists = await _context.ActivityEvents.AnyAsync(e =>
                e.ServiceKey == serviceKey &&
                e.EntityId == entityId &&
                e.ActivityType == activityType);

            if (exists)
                return;

            _context.ActivityEvents.Add(new ActivityEvent
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                ServiceKey = serviceKey,
                ActivityType = activityType,
                Status = status,
                ResultSummary = resultSummary,
                OccurredAt = occurredAt,
                EntityId = entityId
            });

            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to write ActivityEvent {ActivityType} for user {UserId} entity {EntityId}",
                activityType, userId, entityId);
        }
    }
}
