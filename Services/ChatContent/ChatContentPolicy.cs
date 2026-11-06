using FrancProject.Dto.ChatContent;
namespace FrancProject.Services.ChatContent;

public static class ChatContentPolicy
{
    public static bool IsValidAvailability(string type, string? availability) => availability == null
        || availability is "available" or "all" or "cancelled"
        || (type == "event" ? availability is "scheduled" or "ongoing" or "started" or "ended"
            : availability is "open" or "closed" or "not_open");

    public static List<string> Validate(string type, ChatContentInput input)
    {
        var errors = new List<string>();
        if (type is not ("event" or "survey")) errors.Add("Invalid content type.");
        if (string.IsNullOrWhiteSpace(input.Name) || input.Name.Length > 200) errors.Add("Name is required (maximum 200 characters).");
        if (string.IsNullOrWhiteSpace(input.Description) || input.Description.Length > 20000) errors.Add("Description is required (maximum 20000 characters).");
        if (input.Status is not ("draft" or "published" or "archived")) errors.Add("Invalid publication status.");
        if (type == "event" && input.StartsAt == null) errors.Add("Event start time is required.");
        if (input.StartsAt.HasValue && input.EndsAt.HasValue && input.EndsAt <= input.StartsAt) errors.Add("End time must be after start time.");
        if (type == "survey" && string.IsNullOrWhiteSpace(input.Url)) errors.Add("Survey URL is required.");
        if (!string.IsNullOrWhiteSpace(input.Url) && (!Uri.TryCreate(input.Url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https") || !string.IsNullOrEmpty(uri.UserInfo))) errors.Add("URL must be an HTTP or HTTPS address without credentials.");
        if (input.Url?.Length > 2000 || input.Location?.Length > 500) errors.Add("URL or location is too long.");
        try { TimeZoneInfo.FindSystemTimeZoneById(input.TimeZone); }
        catch (Exception e) when (e is TimeZoneNotFoundException or InvalidTimeZoneException or ArgumentException) { errors.Add("Invalid timezone."); }
        return errors;
    }
    public static string Availability(string type, bool cancelled, DateTimeOffset? start, DateTimeOffset? end, DateTimeOffset now)
    {
        if (cancelled) return "cancelled";
        if (end.HasValue && end <= now) return type == "survey" ? "closed" : "ended";
        if (type == "survey" && start.HasValue && start > now) return "not_open";
        if (type == "event" && start.HasValue && start <= now) return end.HasValue ? "ongoing" : "started";
        return type == "survey" ? "open" : "scheduled";
    }
}
