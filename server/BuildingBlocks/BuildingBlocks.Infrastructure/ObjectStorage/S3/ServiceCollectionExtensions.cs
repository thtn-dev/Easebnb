using Amazon.S3;
using BuildingBlocks.Application.ObjectStorage.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace BuildingBlocks.Infrastructure.ObjectStorage.S3;

public static  class ServiceCollectionExtensions
{
    public static IServiceCollection AddS3ObjectStorage(this IServiceCollection services, IConfiguration config)
    {
        services
            .AddOptions<S3StorageOptions>()
            .Bind(config.GetSection("ObjectStorage:S3"))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddSingleton<IAmazonS3>(sp =>
        {
            var options = sp
                .GetRequiredService<IOptions<S3StorageOptions>>()
                .Value;

            return new AmazonS3Client(
                options.AccessKey,
                options.SecretKey,
                new AmazonS3Config
                {
                    ServiceURL = options.Endpoint,
                    ForcePathStyle = options.ForcePathStyle,
                    AuthenticationRegion = options.Region
                });
        });

        services.AddScoped<IObjectStorage, S3ObjectStorage>();
        return services;
    }
}