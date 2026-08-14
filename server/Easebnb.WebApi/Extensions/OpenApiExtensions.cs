using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi;

namespace Easebnb.WebApi.Extensions;


public static class OpenApiExtensions
{
    public static void ConfigureOpenApi(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
        services.AddOpenApi(options =>
        {
            options.AddDocumentTransformer((document, _, _) =>
            {
                document.Info = new OpenApiInfo
                {
                    Title = "Web API",
                    Version = "v1",
                    Description = "API for managing Web functionalities",
                    Contact = new OpenApiContact
                    {
                        Name = "Web Support",
                        Email = "thtn.1611.dev@gmail.com"
                    }
                };

                // Add Security Scheme
                document.Components ??= new OpenApiComponents();
                document.Components.SecuritySchemes = new Dictionary<string, IOpenApiSecurityScheme>
                {
                    ["Bearer"] = new OpenApiSecurityScheme()
                    {
                        Type = SecuritySchemeType.Http,
                        Scheme = "bearer",
                        BearerFormat = "JWT",
                        Description = "Enter JWT Bearer token in the format: Bearer {your token}"
                    }
                };

                return Task.CompletedTask;
            });

            // Add operation transformers for security and documentation
            options.AddOperationTransformer((operation, context, _) =>
            {
                // Add security requirement for endpoints with [Authorize]
                var metadata = context.Description.ActionDescriptor.EndpointMetadata;
                var hasAuthorize = metadata.OfType<IAuthorizeData>().Any();

                if (!hasAuthorize) return Task.CompletedTask;
                operation.Security ??= [];
                operation.Security.Add(new OpenApiSecurityRequirement
                {
                    [new OpenApiSecuritySchemeReference("Bearer")] = []
                });

                return Task.CompletedTask;
            });
        });
    }
}