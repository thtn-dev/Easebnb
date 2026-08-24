using Easebnb.Organization.Core.Common;

namespace Easebnb.Organization.UnitTests.Common;

public class OrganizationSlugTests
{
    // ---------------------------------------------------------------
    // IsValid
    // ---------------------------------------------------------------

    [Theory]
    [InlineData("a")]
    [InlineData("my-hotel")]
    [InlineData("hotel-2026")]
    [InlineData("a-1-b-2")]
    public void IsValid_WhenSlugMatchesFormat_ReturnsTrue(string slug)
    {
        OrganizationSlug.IsValid(slug).Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("-abc")]
    [InlineData("abc-")]
    [InlineData("My-Hotel")]
    [InlineData("my hotel")]
    [InlineData("my_hotel")]
    [InlineData("my--hotel")]
    [InlineData("my.hotel")]
    [InlineData("mỹ-hotel")]
    public void IsValid_WhenSlugViolatesFormat_ReturnsFalse(string slug)
    {
        OrganizationSlug.IsValid(slug).Should().BeFalse();
    }

    [Fact]
    public void IsValid_WhenSlugExceedsMaxLength_ReturnsFalse()
    {
        var slug = new string('a', OrganizationSlug.MaxLength + 1);

        OrganizationSlug.IsValid(slug).Should().BeFalse();
    }

    [Fact]
    public void IsValid_WhenSlugIsExactlyMaxLength_ReturnsTrue()
    {
        var slug = new string('a', OrganizationSlug.MaxLength);

        OrganizationSlug.IsValid(slug).Should().BeTrue();
    }

    // ---------------------------------------------------------------
    // Normalize
    // ---------------------------------------------------------------

    [Fact]
    public void Normalize_TrimsAndLowercases()
    {
        OrganizationSlug.Normalize("  My-Hotel ").Should().Be("my-hotel");
    }

    // ---------------------------------------------------------------
    // FromName
    // ---------------------------------------------------------------

    [Theory]
    [InlineData("My Hotel", "my-hotel")]
    [InlineData("Sơn Trà Hotel", "son-tra-hotel")]
    [InlineData("Đại Dương", "dai-duong")]
    [InlineData("Hotel 2026!!", "hotel-2026")]
    public void FromName_WhenNameContainsSeparatorsOrDiacritics_ProducesKebabSlug(string name, string expected)
    {
        OrganizationSlug.FromName(name).Should().Be(expected);
    }

    [Fact]
    public void FromName_WhenSeparatorsRepeat_CollapsesIntoSingleHyphen()
    {
        OrganizationSlug.FromName("My  --  Hotel").Should().Be("my-hotel");
    }

    [Fact]
    public void FromName_WhenNameIsLong_TruncatesToMaxLengthWithoutTrailingHyphen()
    {
        var name = new string('a', OrganizationSlug.MaxLength - 1) + " b";

        var slug = OrganizationSlug.FromName(name);

        slug.Length.Should().Be(OrganizationSlug.MaxLength - 1,
            "the trailing hyphen produced by truncation must be trimmed");
        slug.Should().EndWith("a");
    }

    [Fact]
    public void FromName_WhenNothingUsableRemains_FallsBackToOrgPrefix()
    {
        var slug = OrganizationSlug.FromName("ホテル");

        slug.Should().StartWith("org-");
        slug.Length.Should().Be("org-".Length + 8);
    }
}
