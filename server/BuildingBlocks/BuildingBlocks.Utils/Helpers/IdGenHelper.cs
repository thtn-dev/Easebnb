namespace Dayline.BuildingBlocks.Utils.Helpers;

public static class IdGenHelper
{
    public static Guid NewGuidId()
    {
        return Guid.CreateVersion7(DateTimeHelper.GetDateTimeNow());
    }
}