using FrancProject.Dto.ChatContent;
using FrancProject.Services.ChatContent;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FrancProject.Controllers;
[ApiController]
[Route("api/chat-content")]
[Authorize]
public sealed class ChatContentController(ChatContentService service) : ControllerBase
{
    [HttpGet("{type:regex(^(event|survey)$)}")]
    public async Task<IActionResult> List(string type, [FromQuery] int[]? ids, [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to, [FromQuery] int? eventId, [FromQuery] string? query, [FromQuery] int[]? eventIds,
        [FromQuery] bool includeUnavailable = true, [FromQuery] bool admin = false, [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20, CancellationToken ct = default, [FromQuery] string? availability = null)
    {
        if (admin && !User.IsInRole("Admin")) return Forbid();
        if (!ChatContentPolicy.IsValidAvailability(type, availability)) return BadRequest(new { message = "Invalid availability filter for this content type." });
        if (page is < 1 or > 10000 || query?.Length > 2000 || pageSize is < 1 or > 100 || ids?.Length > 100 || eventIds?.Length > 100 || (from.HasValue && to.HasValue && from >= to)) return BadRequest(new { message = "Invalid range or pagination." });
        return Ok(await service.List(type, admin, ids, from, to, eventId, query, includeUnavailable, page, pageSize, ct, eventIds, availability));
    }
    [HttpGet("{type:regex(^(event|survey)$)}/{id:int}")]
    public async Task<IActionResult> Get(string type, int id, CancellationToken ct) => await service.Find(type, id, false, ct) is { } row ? Ok(row) : NotFound();
    [HttpPost("{type:regex(^(event|survey)$)}")]
    [Authorize(Roles = "Admin")]
    public Task<IActionResult> Create(string type, ChatContentInput input, CancellationToken ct) => Save(type, null, input, ct);
    [HttpPut("{type:regex(^(event|survey)$)}/{id:int}")]
    [Authorize(Roles = "Admin")]
    public Task<IActionResult> Update(string type, int id, ChatContentInput input, CancellationToken ct) => Save(type, id, input, ct);
    private async Task<IActionResult> Save(string type, int? id, ChatContentInput input, CancellationToken ct)
    {
        try { return Ok(await service.Save(type, id, input, ct)); }
        catch (ArgumentException e) { return BadRequest(new { message = e.Message }); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (DbUpdateConcurrencyException) { return Conflict(new { message = "This record changed. Reload before saving." }); }
    }
    [HttpPost("{type:regex(^(event|survey)$)}/{id:int}/retry-index")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Retry(string type, int id, CancellationToken ct)
    {
        try { await service.Retry(type, id, ct); return Accepted(); }
        catch (KeyNotFoundException) { return NotFound(); }
    }
}
