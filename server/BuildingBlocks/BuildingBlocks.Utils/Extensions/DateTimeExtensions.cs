namespace Dayline.BuildingBlocks.Utils.Extensions;

public static class DateTimeExtensions
{
    /// <summary>
    ///     Converts DateTime to Unix timestamp
    /// </summary>
    public static long ToUnixTimestamp(this DateTime dateTime)
    {
        var dateTimeOffset = new DateTimeOffset(dateTime.Kind == DateTimeKind.Utc
            ? dateTime
            : dateTime.ToUniversalTime());
        return dateTimeOffset.ToUnixTimeSeconds();
    }

    /// <summary>
    ///     Creates DateTime from Unix timestamp
    /// </summary>
    public static DateTime FromUnixTimestamp(long timestamp)
    {
        return DateTimeOffset.FromUnixTimeSeconds(timestamp).UtcDateTime;
    }

    /// <summary>
    ///     Gets the start of the day
    /// </summary>
    public static DateTime StartOfDay(this DateTime date)
    {
        return date.Date;
    }

    /// <summary>
    ///     Gets the end of the day (23:59:59.999)
    /// </summary>
    public static DateTime EndOfDay(this DateTime date)
    {
        return date.Date.AddDays(1).AddTicks(-1);
    }

    /// <summary>
    ///     Gets the start of the week (Monday)
    /// </summary>
    public static DateTime StartOfWeek(this DateTime date, DayOfWeek startOfWeek = DayOfWeek.Monday)
    {
        var diff = (7 + (date.DayOfWeek - startOfWeek)) % 7;
        return date.AddDays(-diff).Date;
    }

    /// <summary>
    ///     Gets the end of the week (Sunday 23:59:59.999)
    /// </summary>
    public static DateTime EndOfWeek(this DateTime date, DayOfWeek startOfWeek = DayOfWeek.Monday)
    {
        return date.StartOfWeek(startOfWeek).AddDays(7).AddTicks(-1);
    }

    /// <summary>
    ///     Gets the start of the month
    /// </summary>
    public static DateTime StartOfMonth(this DateTime date)
    {
        return new DateTime(date.Year, date.Month, 1, 0, 0, 0, date.Kind);
    }

    /// <summary>
    ///     Gets the end of the month
    /// </summary>
    public static DateTime EndOfMonth(this DateTime date)
    {
        return date.StartOfMonth().AddMonths(1).AddTicks(-1);
    }
}