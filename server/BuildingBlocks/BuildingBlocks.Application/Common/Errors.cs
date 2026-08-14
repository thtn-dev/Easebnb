namespace BuildingBlocks.Application.Common;

//public readonly record struct NotFoundError(string message) : Error(message);

//public class ValidationError(string message) : Error(message);

//public class UnauthorizedError(string message) : Error(message);

//public class ForbiddenError(string message) : Error(message);

//public class ConflictError(string message) : Error(message);

//public class ExternalServiceError(string message) : Error(message);

//public class DatabaseError(string message) : Error(message);

//public class UnexpectedError(string message) : Error(message);

//public static class Errors
//{
//    public static Result NotFound(string message = "The requested resource was not found.")
//    {
//        return Result.Fail(new NotFoundError(message));
//    }

//    public static Result Validation(string message = "One or more validation errors occurred.")
//    {
//        return Result.Fail(new ValidationError(message));
//    }

//    public static Result Validation(IEnumerable<string> messages)
//    {
//        var result = Result.Fail(new ValidationError("One or more validation errors occurred."));
//        foreach (var message in messages) result.WithError(new ValidationError(message));

//        return result;
//    }

//    public static Result Validation(IDictionary<string, string> messages)
//    {
//        var result = Result.Fail(new ValidationError("One or more validation errors occurred."));
//        foreach (var message in messages) result.WithError(new ValidationError($"{message.Key}: {message.Value}"));

//        return result;
//    }

//    public static Result Unauthorized(string message = "You are not authorized to perform this action.")
//    {
//        return Result.Fail(new UnauthorizedError(message));
//    }

//    public static Result Forbidden(string message = "You do not have permission to access this resource.")
//    {
//        return Result.Fail(new ForbiddenError(message));
//    }

//    public static Result Conflict(string message = "A conflict occurred with the current state of the resource.")
//    {
//        return Result.Fail(new ConflictError(message));
//    }

//    public static Result ExternalService(
//        string message = "An error occurred while communicating with an external service.")
//    {
//        return Result.Fail(new ExternalServiceError(message));
//    }

//    public static Result Database(string message = "A database error occurred.")
//    {
//        return Result.Fail(new DatabaseError(message));
//    }

//    public static Result Unexpected(string message = "An unexpected error occurred.")
//    {
//        return Result.Fail(new UnexpectedError(message));
//    }
//}