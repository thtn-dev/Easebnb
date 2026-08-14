using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace BuildingBlocks.Endpoints.Abstractions;

public interface IEndpoint
{
    void MapEndpoint(WebApplication app);
}

/// <summary>
///     Interface for endpoints that belong to a route group.
///     Allows grouping endpoints under a common prefix with shared configuration.
/// </summary>
public interface IEndpointGroup
{
    /// <summary>
    ///     The route prefix for this group (e.g., "/api/users")
    /// </summary>
    string GroupPrefix { get; }

    /// <summary>
    ///     Map all endpoints within this group
    /// </summary>
    void MapEndpoints(RouteGroupBuilder group);
}