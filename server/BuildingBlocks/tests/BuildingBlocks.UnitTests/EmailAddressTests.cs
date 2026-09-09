using BuildingBlocks.Application;

namespace BuildingBlocks.UnitTests;

public class EmailAddressTests
{
    [Theory]
    [InlineData("john@example.com")]
    [InlineData("john.doe@example.com")]
    [InlineData("john+test@example.com")]
    [InlineData("john.doe@example.co.uk")]
    [InlineData("user123@test-domain.com")]
    public void Create_Should_Accept_Valid_Email(string email)
    {
        // Act
        var result = EmailAddress.Create(email);

        // Assert
        result.Should().NotBeNull();
        result.Value.Should().Be(email);
    }

    [Fact]
    public void Create_Should_Normalize_Email()
    {
        // Act
        var result = EmailAddress.Create("  John.Doe@Example.COM  ");

        // Assert
        result.Value.Should().Be("john.doe@example.com");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    public void Create_Should_Throw_When_Email_Is_Null_Or_Whitespace(string? email)
    {
        // Act
        var act = () => EmailAddress.Create(email);

        // Assert
        act.Should()
            .Throw<ArgumentException>()
            .WithParameterName("email");
    }

    [Theory]
    [InlineData("john")]
    [InlineData("@example.com")]
    [InlineData("john@")]
    [InlineData("john@@example.com")]
    [InlineData("john@example")]
    [InlineData("john@localhost")]
    [InlineData("john@.example.com")]
    [InlineData("john@example.com.")]
    [InlineData("john@-example.com")]
    [InlineData("john@example-.com")]
    [InlineData("john@example..com")]
    public void Create_Should_Throw_When_Email_Is_Invalid(string email)
    {
        // Act
        var act = () => EmailAddress.Create(email);

        // Assert
        act.Should()
            .Throw<ArgumentException>()
            .WithParameterName("email");
    }

    [Fact]
    public void Create_Should_Throw_When_Email_Exceeds_Max_Length()
    {
        // Arrange
        var localPart = new string('a', 64);
        var domain = new string('b', 190) + ".com";
        var email = $"{localPart}@{domain}";

        // Act
        var act = () => EmailAddress.Create(email);

        // Assert
        act.Should()
            .Throw<ArgumentException>()
            .WithParameterName("email");
    }

    [Fact]
    public void Create_Should_Throw_When_LocalPart_Exceeds_Max_Length()
    {
        // Arrange
        var localPart = new string('a', 65);
        var email = $"{localPart}@example.com";

        // Act
        var act = () => EmailAddress.Create(email);

        // Assert
        act.Should()
            .Throw<ArgumentException>()
            .WithParameterName("email");
    }

    [Fact]
    public void Create_Should_Throw_When_Domain_Exceeds_Max_Length()
    {
        // Arrange
        var domain = new string('a', 250) + ".com";
        var email = $"user@{domain}";

        // Act
        var act = () => EmailAddress.Create(email);

        // Assert
        act.Should()
            .Throw<ArgumentException>()
            .WithParameterName("email");
    }

    [Fact]
    public void Domain_Should_Return_Domain_Part()
    {
        // Arrange
        var email = EmailAddress.Create("john.doe@example.com");

        // Act
        var result = email.Domain;

        // Assert
        result.Should().Be("example.com");
    }

    [Fact]
    public void LocalPart_Should_Return_Local_Part()
    {
        // Arrange
        var email = EmailAddress.Create("john.doe@example.com");

        // Act
        var result = email.LocalPart;

        // Assert
        result.Should().Be("john.doe");
    }

    [Fact]
    public void ToString_Should_Return_Email_Value()
    {
        // Arrange
        var email = EmailAddress.Create("john@example.com");

        // Act
        var result = email.ToString();

        // Assert
        result.Should().Be("john@example.com");
    }

    [Fact]
    public void TryCreate_Should_Return_True_For_Valid_Email()
    {
        // Act
        var result = EmailAddress.TryCreate(
            "John@Example.com",
            out var emailAddress);

        // Assert
        result.Should().BeTrue();
        emailAddress.Should().NotBeNull();
        emailAddress!.Value.Should().Be("john@example.com");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("invalid")]
    [InlineData("john@localhost")]
    [InlineData("john@@example.com")]
    public void TryCreate_Should_Return_False_For_Invalid_Email(
        string? email)
    {
        // Act
        var result = EmailAddress.TryCreate(
            email,
            out var emailAddress);

        // Assert
        result.Should().BeFalse();
        emailAddress.Should().BeNull();
    }

    [Fact]
    public void Explicit_String_Conversion_Should_Create_EmailAddress()
    {
        // Act
        var email = (EmailAddress)"John@Example.com";

        // Assert
        email.Value.Should().Be("john@example.com");
    }

    [Fact]
    public void Implicit_EmailAddress_Conversion_Should_Return_String()
    {
        // Arrange
        var email = EmailAddress.Create("john@example.com");

        // Act
        string result = email;

        // Assert
        result.Should().Be("john@example.com");
    }

    [Fact]
    public void EmailAddress_Should_Be_Equal_When_Values_Are_Equal()
    {
        // Arrange
        var first = EmailAddress.Create("John@Example.com");
        var second = EmailAddress.Create("john@example.com");

        // Assert
        first.Should().Be(second);
    }
}