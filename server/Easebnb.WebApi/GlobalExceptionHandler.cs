using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Easebnb.WebApi;


public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
        => _logger = logger;

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var traceId = Activity.Current?.TraceId.ToString()
                      ?? httpContext.TraceIdentifier;

        var (statusCode, title) = MapException(exception);

        _logger.LogError(
            exception,
            "Unhandled exception [{ExceptionType}] | TraceId: {TraceId} | Path: {Path}",
            exception.GetType().Name,
            traceId,
            httpContext.Request.Path
        );

        var problemDetails = new ProblemDetails
        {
            Status   = statusCode,
            Title    = title,
            Detail   = exception.Message,
            Instance = httpContext.Request.Path,
            Extensions =
            {
                ["traceId"] = traceId
            }
        };
        if (exception is ValidationException validationEx)
            problemDetails.Extensions["errors"] = validationEx.Errors;

        httpContext.Response.StatusCode  = statusCode;
        httpContext.Response.ContentType = "application/problem+json";

        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

        return true;
    }

    
    private static (int StatusCode, string Title) MapException(Exception exception) =>
        exception switch
        {
            AppException app => (app.StatusCode, app.Title),

            ArgumentNullException       => (StatusCodes.Status400BadRequest,     "Bad Request"),
            ArgumentException           => (StatusCodes.Status400BadRequest,     "Bad Request"),
            UnauthorizedAccessException => (StatusCodes.Status401Unauthorized,   "Unauthorized"),
            KeyNotFoundException        => (StatusCodes.Status404NotFound,       "Not Found"),
            NotImplementedException     => (StatusCodes.Status501NotImplemented, "Not Implemented"),
            OperationCanceledException  => (StatusCodes.Status499ClientClosedRequest, "Client Closed Request"),
            _                           => (StatusCodes.Status500InternalServerError, "Internal Server Error")
        };
}

public abstract class AppException(string message, int statusCode, string title) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
    public string Title   { get; } = title;
}

public class NotFoundException(string resource, object key) : AppException(
    $"{resource} with key '{key}' was not found.",
    StatusCodes.Status404NotFound,
    "Not Found");

public class ValidationException(IEnumerable<string> errors) : AppException("One or more validation errors occurred.",
    StatusCodes.Status400BadRequest,
    "Validation Error")
{
    public IEnumerable<string> Errors { get; } = errors;
}

public class ConflictException(string message) : AppException(message, StatusCodes.Status409Conflict, "Conflict");