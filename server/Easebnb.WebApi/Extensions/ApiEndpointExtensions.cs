using BuildingBlocks.Application;
using BuildingBlocks.Endpoints;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Easebnb.WebApi.Extensions;

/// <summary>
///     Extension methods for building consistent API endpoints
/// </summary>
public static class EndpointExtensions
{
    extension(RouteHandlerBuilder builder)
    {
        /// <summary>
        ///     Adds standard API metadata to an endpoint
        /// </summary>
        public RouteHandlerBuilder WithApiMetadata(string summary,
            string? description = null,
            params string[] tags)
        {
            builder.WithSummary(summary);

            if (!string.IsNullOrEmpty(description)) builder.WithDescription(description);

            if (tags.Length > 0) builder.WithTags(tags);

            return builder;
        }

        /// <summary>
        ///     Adds common response types to endpoint metadata.
        ///     Supports 200, 400, 404, and 500 responses
        /// </summary>
        public RouteHandlerBuilder WithStandardResponses<T>()
        {
            return builder
                .Produces<ApiResponse<T>>(StatusCodes.Status200OK)
                .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
                .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
                .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError);
        }

        public RouteHandlerBuilder WithStandardResponses()
        {
            return builder
                .Produces<ApiResponse>(StatusCodes.Status200OK)
                .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
                .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
                .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError);
        }

        /// <summary>
        ///     Adds paginated response types to endpoint metadata
        /// </summary>
        public RouteHandlerBuilder WithPaginatedResponses<T>()
        {
            return builder
                .Produces<PaginatedResponse<T>>()
                .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
                .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError);
        }
    }


    /// <summary>
    ///     Validates pagination parameters
    /// </summary>
    public static bool ValidatePagination(int page, int pageSize, out IResult? errorResult)
    {
        if (page < 1 || pageSize is < 1 or > 100)
        {
            errorResult = Results.BadRequest();
            return false;
        }

        errorResult = null;
        return true;
    }
}

/// <summary>
///     Extension methods for registering Api services
/// </summary>
public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>
        ///     Adds all Api services including:
        ///     - IHttpContextAccessor
        ///     - ICurrentUserAccessor
        ///     - Endpoint registration from the specified assembly marker type
        /// </summary>
        public IServiceCollection AddApi<TAssemblyMarker>()
        {
            services.AddApiCore()
                .RegisterEndpointsFromAssemblyContaining<TAssemblyMarker>();
            return services;
        }

        /// <summary>
        ///     Adds core Api services without endpoint registration:
        ///     - IHttpContextAccessor
        ///     - ICurrentUserAccessor
        /// </summary>
        private IServiceCollection AddApiCore()
        {
            services.TryAddSingleton<IHttpContextAccessor, HttpContextAccessor>();
            services.TryAddScoped<ICurrentUserAccessor, CurrentUserAccessor>();
            return services;
        }
    }
}