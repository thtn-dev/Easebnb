using BuildingBlocks.Application;
using ErrorOr;
using FluentValidation;
using FluentValidation.Results;
using MediatR;

namespace BuildingBlocks.UnitTests;

public class ValidationBehaviorTests
{
    private readonly Mock<IValidator<TestRequest>> _validatorMock = new();
    private readonly ValidationBehavior<TestRequest, ErrorOr<string>> _sut;

    public ValidationBehaviorTests()
    {
        _sut = new ValidationBehavior<TestRequest, ErrorOr<string>>([_validatorMock.Object]);
    }

    public sealed record TestRequest(string Name) : IRequest<ErrorOr<string>>;

    private sealed class NextTracker
    {
        public int Calls { get; private set; }

        public RequestHandlerDelegate<ErrorOr<string>> ToDelegate(string value = "ok") =>
            _ =>
            {
                Calls++;
                return Task.FromResult<ErrorOr<string>>(value);
            };
    }

    private static ValidationFailure CreateFailure(
        string propertyName = "Name",
        string errorMessage = "Name is required",
        object? attemptedValue = null) =>
        new(propertyName, errorMessage) { ErrorCode = "NotEmptyValidator", AttemptedValue = attemptedValue };


    // ---------------------------------------------------------------
    // Handle - no validators
    // ---------------------------------------------------------------

    [Fact]
    public async Task Handle_WhenNoValidatorsRegistered_CallsNextWithoutValidating()
    {
        var sut = new ValidationBehavior<TestRequest, ErrorOr<string>>([]);
        var request = new TestRequest("hello");
        var next = new NextTracker();

        var result = await sut.Handle(request, next.ToDelegate(), CancellationToken.None);

        next.Calls.Should().Be(1);
        result.IsError.Should().BeFalse();
        result.Value.Should().Be("ok");
    }

    // ---------------------------------------------------------------
    // Handle - validation passes
    // ---------------------------------------------------------------

    [Fact]
    public async Task Handle_WhenValidatorPasses_CallsNextWithRequestResult()
    {
        var request = new TestRequest("hello");
        var next = new NextTracker();
        _validatorMock
            .Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<TestRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        var result = await _sut.Handle(request, next.ToDelegate(), CancellationToken.None);

        next.Calls.Should().Be(1, "a passing validator must not short-circuit the pipeline");
        result.IsError.Should().BeFalse();
        result.Value.Should().Be("ok");
    }

    // ---------------------------------------------------------------
    // Handle - validation fails
    // ---------------------------------------------------------------

    [Fact]
    public async Task Handle_WhenValidatorFails_ReturnsValidationErrorsWithoutCallingNext()
    {
        var request = new TestRequest("");
        var next = new NextTracker();
        _validatorMock
            .Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<TestRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult([CreateFailure(attemptedValue: "")]));

        var result = await _sut.Handle(request, next.ToDelegate(), CancellationToken.None);

        next.Calls.Should().Be(0, "validation failures must short-circuit before the handler runs");
        result.IsError.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.Validation);
        result.FirstError.Code.Should().Be("NotEmptyValidator");
        result.FirstError.Description.Should().Be("Name is required");
        result.FirstError.Metadata.Should().NotBeNull();
        result.FirstError.Metadata!["PropertyName"].Should().Be("Name");
        result.FirstError.Metadata["AttemptedValue"].Should().Be("");
    }

    // ---------------------------------------------------------------
    // Handle - multiple validators
    // ---------------------------------------------------------------

    [Fact]
    public async Task Handle_WhenMultipleValidatorsFail_AggregatesAllFailures()
    {
        var secondValidatorMock = new Mock<IValidator<TestRequest>>();
        var sut = new ValidationBehavior<TestRequest, ErrorOr<string>>([_validatorMock.Object, secondValidatorMock.Object]);
        var request = new TestRequest("");
        var next = new NextTracker();
        _validatorMock
            .Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<TestRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult([CreateFailure("Name", "Name is required")]));
        secondValidatorMock
            .Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<TestRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult([CreateFailure("Age", "Age must be positive")]));

        var result = await sut.Handle(request, next.ToDelegate(), CancellationToken.None);

        next.Calls.Should().Be(0);
        result.IsError.Should().BeTrue();
        result.Errors.Should().HaveCount(2, "failures from every validator must be merged");
        result.Errors.Should().Contain(e => e.Description == "Name is required");
        result.Errors.Should().Contain(e => e.Description == "Age must be positive");
    }

    [Fact]
    public async Task Handle_WhenOneValidatorFailsAmongPassing_ReturnsItsFailure()
    {
        var passingValidatorMock = new Mock<IValidator<TestRequest>>();
        var sut = new ValidationBehavior<TestRequest, ErrorOr<string>>([_validatorMock.Object, passingValidatorMock.Object]);
        var request = new TestRequest("x");
        var next = new NextTracker();
        _validatorMock
            .Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<TestRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult([CreateFailure("Name", "Name is too short")]));
        passingValidatorMock
            .Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<TestRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        var result = await sut.Handle(request, next.ToDelegate(), CancellationToken.None);

        next.Calls.Should().Be(0);
        result.IsError.Should().BeTrue();
        result.Errors.Should().ContainSingle()
            .Which.Description.Should().Be("Name is too short");
    }
}
