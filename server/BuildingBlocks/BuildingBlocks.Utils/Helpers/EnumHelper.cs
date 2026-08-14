namespace Dayline.BuildingBlocks.Utils.Helpers;

public static class EnumHelper
{
    public static IEnumerable<KeyValuePair<TEnum, TKey>> GetEnumKeyValue<TEnum, TKey>()
        where TEnum : struct, Enum
        where TKey : class
    {
        var (keys, names) = GetMetadata<TEnum, TKey>();

        var enumValues = Enum.GetValues(typeof(TEnum)).Cast<TEnum>();

        var results = enumValues
            .Zip(keys, (enumValue, key) => new KeyValuePair<TEnum, TKey>(enumValue, key));

        return results;
    }

    public static (IEnumerable<TKey>, IEnumerable<string>) GetMetadata<TEnum, TKey>()
        where TEnum : struct, Enum
    {
        var keys = Enum.GetValues<TEnum>()
            .Select(e => (TKey)Convert.ChangeType(e, typeof(TKey)))
            .ToList();

        var names = Enum.GetNames<TEnum>().ToList();

        return (keys, names);
    }
}