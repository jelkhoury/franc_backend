using FrancProject.Dto.AdminAnalytics;
using FrancProject.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FrancProject.Controllers;

[ApiController]
[Route("api/admin/analytics")]
[Authorize(Roles = "Admin")]
public class AdminAnalyticsController : ControllerBase
{
    private readonly IAdminAnalyticsService _analytics;

    public AdminAnalyticsController(IAdminAnalyticsService analytics)
    {
        _analytics = analytics;
    }

    [HttpGet("overview")]
    public async Task<IActionResult> GetOverview([FromQuery] AnalyticsQueryDto query)
    {
        return Ok(await _analytics.GetOverviewAsync(query));
    }

    [HttpGet("activity-trend")]
    public async Task<IActionResult> GetActivityTrend([FromQuery] AnalyticsQueryDto query)
    {
        return Ok(await _analytics.GetActivityTrendAsync(query));
    }

    [HttpGet("recent-activity")]
    public async Task<IActionResult> GetRecentActivity([FromQuery] AnalyticsQueryDto query)
    {
        return Ok(await _analytics.GetRecentActivityAsync(query));
    }

    [HttpGet("services/{serviceKey}")]
    public async Task<IActionResult> GetServiceAnalytics(string serviceKey, [FromQuery] AnalyticsQueryDto query)
    {
        try
        {
            return Ok(await _analytics.GetServiceAnalyticsAsync(serviceKey, query));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("users")]
    public async Task<IActionResult> GetUsers([FromQuery] AnalyticsQueryDto query)
    {
        return Ok(await _analytics.GetUsersAsync(query));
    }

    [HttpGet("users/{userId:int}")]
    public async Task<IActionResult> GetUser360(int userId, [FromQuery] AnalyticsQueryDto query)
    {
        var result = await _analytics.GetUser360Async(userId, query);
        if (result == null)
            return NotFound(new { message = "User not found." });

        return Ok(result);
    }
}
