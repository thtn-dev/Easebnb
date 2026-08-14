namespace Dayline.BuildingBlocks.Utils.Helpers;

public static class DateTimeHelper
{
    public static DateTime GetDateTimeNow()
    {
        return DateTimeOffset.Now.UtcDateTime;
    }
}