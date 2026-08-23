using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Easebnb.Organization.Core.Common;

/// <summary>
///     Slug rules for organizations: lowercase ascii letters, digits and
///     single hyphens ("my-hotel-group"), at most 100 characters. Kept in
///     one place so endpoint validators and application services share the
///     exact same format.
/// </summary>
public static partial class OrganizationSlug
{
    public const int MaxLength = 100;

    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$")]
    private static partial Regex SlugPattern();

    public static bool IsValid(string slug)
    {
        return slug.Length <= MaxLength && SlugPattern().IsMatch(slug);
    }

    public static string Normalize(string slug)
    {
        return slug.Trim().ToLowerInvariant();
    }

    /// <summary>
    ///     Derives a slug from an organization name: strips diacritics
    ///     (Vietnamese included), lowercases and kebab-cases the result.
    ///     Falls back to a random suffix when nothing usable remains
    ///     (e.g. the name contains only non-latin characters).
    /// </summary>
    public static string FromName(string name)
    {
        var builder = new StringBuilder();

        foreach (var c in name.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
                continue;

            var candidate = c switch
            {
                'Đ' or 'đ' => 'd',
                _ => char.ToLowerInvariant(c)
            };

            builder.Append(char.IsAsciiLetterOrDigit(candidate) ? candidate : '-');
        }

        var slug = Regex.Replace(builder.ToString(), "-{2,}", "-").Trim('-');

        if (slug.Length > MaxLength)
            slug = slug[..MaxLength].Trim('-');

        return slug.Length == 0
            ? $"org-{Guid.NewGuid().ToString("N")[..8]}"
            : slug;
    }
}
