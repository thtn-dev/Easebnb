using BuildingBlocks.Application;

namespace BuildingBlocks.UnitTests;


public class UserIdTests
{
    [Fact]
    public void New_Should_Create_NonEmpty_UserId()
    {
        // Act
        var userId = UserId.New();

        // Assert
        userId.Value.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void From_Should_Create_UserId_With_Given_Value()
    {
        // Arrange
        var value = Guid.NewGuid();

        // Act
        var userId = UserId.From(value);

        // Assert
        userId.Value.Should().Be(value);
    }

    [Fact]
    public void From_Should_Throw_When_Value_Is_Empty()
    {
        // Act
        var act = () => UserId.From(Guid.Empty);

        // Assert
        act.Should()
            .Throw<ArgumentException>()
            .WithParameterName("value")
            .WithMessage("UserId cannot be empty.*");
    }

    [Fact]
    public void ToString_Should_Return_Guid_Value()
    {
        // Arrange
        var value = Guid.NewGuid();
        var userId = UserId.From(value);

        // Act
        var result = userId.ToString();

        // Assert
        result.Should().Be(value.ToString());
    }

    [Fact]
    public void UserId_Should_Be_Equal_When_Values_Are_Equal()
    {
        // Arrange
        var value = Guid.NewGuid();

        var first = UserId.From(value);
        var second = UserId.From(value);

        // Assert
        first.Should().Be(second);
    }
}