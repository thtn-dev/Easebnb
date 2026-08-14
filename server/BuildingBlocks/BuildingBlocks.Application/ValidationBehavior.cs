using ErrorOr;
using FluentValidation;
using MediatR;

namespace BuildingBlocks.Application;

public sealed class ValidationBehavior<TRequest, TResponse>(
    IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
    where TResponse : IErrorOr
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!validators.Any()) return await next(cancellationToken);

        var context = new ValidationContext<TRequest>(request);

        var validationResults = await Task.WhenAll(
            validators.Select(v => v.ValidateAsync(context, cancellationToken)));

        var errors = validationResults
            .SelectMany(r => r.Errors)
            .Where(f => f is not null)
            .Select(f => Error.Validation(
                f.ErrorCode,
                f.ErrorMessage,
                new Dictionary<string, object>
                {
                    ["PropertyName"] = f.PropertyName,
                    ["AttemptedValue"] = f.AttemptedValue
                }))
            .ToList();

        if (errors.Count == 0) return await next(cancellationToken);

        return (dynamic)errors;
    }
}