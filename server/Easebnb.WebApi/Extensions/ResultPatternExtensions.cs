using System.Diagnostics;
using BuildingBlocks.Application;
using ErrorOr;
using Microsoft.AspNetCore.Mvc;

namespace Easebnb.WebApi.Extensions;

public static class ResultPatternExtensions
{
    extension<T>(ErrorOr<T> result)
    {
        /// <summary>
        ///     Converts an ErrorOr result into an HTTP result.
        /// </summary>
        public IResult ToHttpResult()
        {
            if (result.IsError)
                return result.Errors.ToProblemDetails();

            return Results.Ok(
                ApiResponse<T>.Ok(
                    result.Value,
                    "completed"));
        }
    }

    extension(ErrorOr<Success> result)
    {
        /// <summary>
        ///     Converts an ErrorOr result into an HTTP result.
        /// </summary>
        public IResult ToHttpResult()
        {
            return result.IsError
                ? result.Errors.ToProblemDetails()
                : Results.NoContent();
        }
    }

    private static IResult ToProblemDetails(
        this IReadOnlyList<Error> errors)
    {
        if (errors.Count == 0)
            return Results.Problem(
                new ProblemDetails
                {
                    Status = StatusCodes.Status500InternalServerError,
                    Type = "https://tools.ietf.org/html/rfc7231#section-6.6.1",
                    Title = "Internal Server Error",
                    Detail = "An unexpected error occurred.",
                    Extensions =
                    {
                        ["traceId"] =
                            Activity.Current?.Id ?? Guid.NewGuid().ToString()
                    }
                });

        var error = errors[0];

        return error.ToProblemDetails();
    }

    private static IResult ToProblemDetails(
        this Error error)
    {
        var (statusCode, type, title) = error.Type switch
        {
            ErrorType.Validation => (
                StatusCodes.Status400BadRequest,
                "https://tools.ietf.org/html/rfc7231#section-6.5.1",
                "Validation Error"),

            ErrorType.Unauthorized => (
                StatusCodes.Status401Unauthorized,
                "https://tools.ietf.org/html/rfc7235#section-3.1",
                "Unauthorized"),

            ErrorType.Forbidden => (
                StatusCodes.Status403Forbidden,
                "https://tools.ietf.org/html/rfc7231#section-6.5.3",
                "Forbidden"),

            ErrorType.NotFound => (
                StatusCodes.Status404NotFound,
                "https://tools.ietf.org/html/rfc7231#section-6.5.4",
                "Not Found"),

            ErrorType.Conflict => (
                StatusCodes.Status409Conflict,
                "https://tools.ietf.org/html/rfc7231#section-6.5.8",
                "Conflict"),

            ErrorType.Unexpected => (
                StatusCodes.Status500InternalServerError,
                "https://tools.ietf.org/html/rfc7231#section-6.6.1",
                "Internal Server Error"),

            ErrorType.Failure => (
                StatusCodes.Status500InternalServerError,
                "https://tools.ietf.org/html/rfc7231#section-6.6.1",
                "Failure"),

            _ => (
                StatusCodes.Status500InternalServerError,
                "https://tools.ietf.org/html/rfc7231#section-6.6.1",
                "Internal Server Error")
        };

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Type = type,
            Title = title,
            Detail = error.Description,
            Extensions =
            {
                ["traceId"] =
                    Activity.Current?.Id ?? Guid.NewGuid().ToString()
            }
        };

        if (error.Metadata != null && error.Metadata.Count != 0) problemDetails.Extensions["errors"] = error.Metadata;

        return Results.Problem(problemDetails);
    }
}