using FluentValidation;

namespace Easebnb.WebApi.Filters;

public static class ValidationFilterExtensions
{
    public static RouteHandlerBuilder WithValidation<T>(
        this RouteHandlerBuilder builder) where T : class
    {
        return builder.AddEndpointFilter<ValidationFilter<T>>();
    }
}

public class ValidationFilter<T> : IEndpointFilter where T : class
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var validator = context.HttpContext.RequestServices
            .GetService<IValidator<T>>();

        if (validator is null) return await next(context);

        var request = context.Arguments
            .OfType<T>()
            .FirstOrDefault();

        if (request is null) return await next(context);

        var validationResult = await validator.ValidateAsync(request);

        if (!validationResult.IsValid) return Results.ValidationProblem(validationResult.ToDictionary());

        return await next(context);
    }
}