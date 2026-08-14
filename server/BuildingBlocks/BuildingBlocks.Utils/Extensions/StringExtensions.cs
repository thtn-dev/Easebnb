namespace Dayline.BuildingBlocks.Utils.Extensions;

public static class StringExtensions
{
    public static string? TrimStart(this string? source, string trim,
        StringComparison stringComparison = StringComparison.Ordinal)
    {
        if (source == null) return null;

        var s = source;
        while (s.StartsWith(trim, stringComparison)) s = s[trim.Length..];

        return s;
    }

    public static string TrimAfter(this string source, string trim)
    {
        var index = source.IndexOf(trim, StringComparison.Ordinal);
        if (index > 0) source = source[..index];

        return source;
    }
}