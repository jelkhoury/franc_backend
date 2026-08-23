using System.Globalization;

namespace FrancProject.Services.AdminAnalytics;

/// <summary>
/// Tracks the current ISO week bucket used in analytics cache keys.
/// When the week rolls over, cache keys change so responses refresh automatically.
/// </summary>
public sealed class AdminAnalyticsCacheGeneration
{
    private int _isoWeekYear;
    private int _isoWeek;

    public AdminAnalyticsCacheGeneration()
    {
        RefreshIfNeeded(forceLog: false);
    }

    public string Current => $"{_isoWeekYear}-W{_isoWeek:D2}";

    public bool RefreshIfNeeded(bool forceLog = false)
    {
        var now = DateTime.UtcNow;
        var week = ISOWeek.GetWeekOfYear(now);
        var year = ISOWeek.GetYear(now);

        if (week == _isoWeek && year == _isoWeekYear)
            return false;

        _isoWeek = week;
        _isoWeekYear = year;
        return true;
    }
}
