namespace BuildingBlocks.Application;

/// <summary>
///     Provides access to the current authenticated user from the HTTP context.
/// </summary>
public interface ICurrentUserAccessor
{
    /// <summary>
    ///     Returns true if the current request is from an authenticated user.
    /// </summary>
    bool IsAuthenticated { get; }

    /// <summary>
    ///     Gets the current authenticated user, or null if not authenticated.
    /// </summary>
    CurrentUser? GetCurrentUser();

    /// <summary>
    ///     Gets the current authenticated user. Throws if not authenticated.
    /// </summary>
    CurrentUser GetRequiredCurrentUser();
}