namespace GS_CookieOrder_Tracker.Helpers;

public static class DateTimeExtensions
{
    private static readonly TimeZoneInfo PacificZone =
        TimeZoneInfo.FindSystemTimeZoneById("America/Los_Angeles");

    /// <summary>
    /// Converts a UTC DateTime to Pacific Time (America/Los_Angeles).
    /// Handles both PST (UTC-8) and PDT (UTC-7) automatically.
    /// </summary>
    public static DateTime ToPacific(this DateTime utcDateTime)
    {
        var dt = DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc);
        return TimeZoneInfo.ConvertTimeFromUtc(dt, PacificZone);
    }

    /// <summary>
    /// Converts a nullable UTC DateTime to Pacific Time.
    /// Returns null if the input is null.
    /// </summary>
    public static DateTime? ToPacific(this DateTime? utcDateTime)
    {
        if (utcDateTime == null) return null;
        return utcDateTime.Value.ToPacific();
    }
}
