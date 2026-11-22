using System.Globalization;

namespace Blyant;

/// <summary>
/// Times in Blyant: taken in UTC (DateTime.UtcNow, and the UTC times of files) and kept in UTC, also in the lists
/// written to files; made local time only where they are shown to the user or written for a person to read (a
/// timestamp in a text, $date, the log, the name of a file in the trash), with Local, as the last step. Never
/// DateTime.Now: a time kept in local time is ambiguous in the hour the clocks go back, and moves when the time zone
/// changes
/// </summary>
public static class IsoDateTime
{
    // The mark after a time written in UTC in a list ("2026-11-22 09:05Z"); a time without it was written in local
    // time (before 1.6.2)
    private const string UtcMark = "Z";

    /// <summary>
    /// A time as the user reads it: in local time (a time taken in UTC is converted; a local one, or one of no kind as
    /// a test gives it, is as it is)
    /// </summary>
    internal static DateTime Local(DateTime time) => time.Kind == DateTimeKind.Utc ? time.ToLocalTime() : time;

    /// <summary>
    /// A time in local time, written in a format the same in all cultures ("yyyy-MM-dd HH:mm")
    /// </summary>
    internal static string Local(DateTime time, string format) =>
        Local(time).ToString(format, CultureInfo.InvariantCulture);

    /// <summary>
    /// Today, as the user's calendar has it
    /// </summary>
    internal static DateTime Today => DateTime.UtcNow.ToLocalTime().Date;

    /// <summary>
    /// A time for a list kept in a file: in UTC, with "Z" after it ("2026-11-22 09:05Z"). A time of no kind is local
    /// time, as Local takes it
    /// </summary>
    internal static string Stored(DateTime time, string format) =>
        (time.Kind == DateTimeKind.Utc ? time : time.ToUniversalTime()).ToString(format, CultureInfo.InvariantCulture) +
        UtcMark;

    /// <summary>
    /// A time read from a list kept in a file, as UTC: written with "Z" (UTC), or without it (local time, as Blyant
    /// wrote it before 1.6.2). False for a text that is not a time in the format
    /// </summary>
    internal static bool TryParseStored(string text, string format, out DateTime time)
    {
        text = text.Trim();
        var utc = text.EndsWith(UtcMark, StringComparison.Ordinal);
        if (utc) text = text[..^UtcMark.Length];
        if (!DateTime.TryParseExact(text, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out time)) return false;
        time = utc ? DateTime.SpecifyKind(time, DateTimeKind.Utc) : DateTime.SpecifyKind(time, DateTimeKind.Local).ToUniversalTime();
        return true;
    }

    /// <summary>
    /// The time now as ISO-8601 in local time (yyyy-MM-dd HH:mm:ss), the same in all cultures: for :timestamp in the
    /// editor and the macros. The time is read once: read for each part, a date written at midnight could have the
    /// day of one day and the month or year of the next ("2026-12-31" + a second = "2026-01-01")
    /// </summary>
    public static string GetDateTimeNow() => Local(DateTime.UtcNow, "yyyy-MM-dd HH:mm:ss");
}
