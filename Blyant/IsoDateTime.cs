namespace Blyant;

/// <summary>
/// The time now as it is written in the settings file, by :timestamp in the editor and in the macros
/// </summary>
public static class IsoDateTime
{
    #region ISO 8601 Timestamp

    // The time now as ISO-8601 (yyyy-MM-dd HH:mm:ss), the same in all cultures. The time is read once: read for each part, a date written at midnight could have the day of one day and the
    // month or year of the next ("2026-12-31" + a second = "2026-01-01")
    public static string GetDateTimeNow()
    {
        return DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
    }

    #endregion
}