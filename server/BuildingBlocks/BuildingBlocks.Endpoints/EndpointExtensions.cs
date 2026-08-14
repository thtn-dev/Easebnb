using BuildingBlocks.Endpoints.Abstractions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Endpoints;

public static class MapEndpointExtensions
{
    /// <summary>
    ///     Registers all IEndpoint and IEndpointGroup implementations from the assembly containing T.
    /// </summary>
    public static IServiceCollection RegisterEndpointsFromAssemblyContaining<T>(this IServiceCollection services)
    {
        var assembly = typeof(T).Assembly;

        var endpointTypes = assembly.GetTypes()
            .Where(t => t.IsAssignableTo(typeof(IEndpoint)) &&
                        t is { IsClass: true, IsAbstract: false, IsInterface: false });

        var endpointDescriptors = endpointTypes
            .Select(type => ServiceDescriptor.Transient(typeof(IEndpoint), type))
            .ToArray();

        services.TryAddEnumerable(endpointDescriptors);

        var groupTypes = assembly.GetTypes()
            .Where(t => t.IsAssignableTo(typeof(IEndpointGroup)) &&
                        t is { IsClass: true, IsAbstract: false, IsInterface: false });

        var groupDescriptors = groupTypes
            .Select(type => ServiceDescriptor.Transient(typeof(IEndpointGroup), type))
            .ToArray();

        services.TryAddEnumerable(groupDescriptors);

        return services;
    }

    /// <summary>
    ///     Maps all registered IEndpoint and IEndpointGroup implementations.
    /// </summary>
    public static WebApplication MapEndpoints(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var services = scope.ServiceProvider;

        var loggerFactory = services.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger(typeof(MapEndpointExtensions));
        // Map individual endpoints
        var endpoints = app.Services.GetRequiredService<IEnumerable<IEndpoint>>();
        var enumerable = endpoints as IEndpoint[] ?? endpoints.ToArray();
        foreach (var endpoint in enumerable) endpoint.MapEndpoint(app);
        // Map endpoint groups
        var groups = app.Services.GetRequiredService<IEnumerable<IEndpointGroup>>();
        var endpointGroups = groups as IEndpointGroup[] ?? groups.ToArray();
        foreach (var group in endpointGroups)
        {
            var routeGroup = app.MapGroup(group.GroupPrefix)
                .WithTags(group.GroupPrefix);
            group.MapEndpoints(routeGroup);
        }

        // log total endpoints mapped
        logger.LogInformation("Endpoints mapped: {EndpointCount} endpoints and {GroupCount} groups", enumerable.Count(),
            endpointGroups.Count());
        return app;
    }
}