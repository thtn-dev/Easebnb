using System.Globalization;
using System.Text.RegularExpressions;
using BuildingBlocks.Infrastructure.ObjectStorage.S3;

namespace BuildingBlocks.UnitTests;

public class ObjectKeyGeneratorTests
{
    [Fact]
    public void NewKey_WhenCalled_ReturnsUtcDateGuidAndExtension()
    {
        var key = ObjectKeyGenerator.NewKey("photo.png");

        // yyyy/MM/dd/{32 hex chars}.png
        key.Should().MatchRegex(@"^\d{4}/\d{2}/\d{2}/[0-9a-f]{32}\.png$");
        DateTime.TryParseExact(key[..10], "yyyy/MM/dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _)
            .Should().BeTrue("the first ten characters must be the current UTC date");
    }

    [Theory]
    [InlineData("archive.tar.gz", ".gz")]
    [InlineData("avatar.PNG", ".PNG")]
    [InlineData("no-extension", "")]
    public void NewKey_WhenFileNameHasExtension_PreservesOriginalExtension(string fileName, string expectedExtension)
    {
        var key = ObjectKeyGenerator.NewKey(fileName);

        var fileNamePart = key[(key.LastIndexOf('/') + 1)..];
        fileNamePart.Should().EndWith(expectedExtension);
        fileNamePart.Length.Should().Be(32 + expectedExtension.Length);
    }

    [Fact]
    public void NewKey_WhenCalledTwice_GeneratesUniqueKeys()
    {
        var first = ObjectKeyGenerator.NewKey("photo.png");
        var second = ObjectKeyGenerator.NewKey("photo.png");

        first.Should().NotBe(second, "every key embeds a fresh GUID");
    }
}
