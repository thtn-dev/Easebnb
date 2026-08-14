using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Easebnb.Database;

public static class DatabaseServiceExtensions
{
    extension(IServiceCollection services)
    {
        public void AddDatabase<TDbContext>(string moduleName, Action<DatabaseSettings>? configureDbSettings = null)
            where TDbContext : DbContext
        {
            services.AddDbContextPool<TDbContext>((serviceProvider, options) =>
            {
                var dbOptions = serviceProvider.GetRequiredService<IOptions<DatabaseSettings>>().Value;
                configureDbSettings?.Invoke(dbOptions);
                var connectionString = BuildConnectionString(dbOptions, moduleName);

                options.UseNpgsql(connectionString, npgsqlOptions =>
                    {
                        //npgsqlOptions.EnableRetryOnFailure(
                        //    3,
                        //    TimeSpan.FromSeconds(10),
                        //    null);

                        npgsqlOptions.CommandTimeout(30);
                        npgsqlOptions.MigrationsAssembly(typeof(TDbContext).Assembly.FullName);
                    })
                    .UseSnakeCaseNamingConvention();

                if (dbOptions.EnableDetailedErrors) options.EnableDetailedErrors(dbOptions.EnableDetailedErrors);
                if (dbOptions.EnableSensitiveDataLogging)
                    options.EnableSensitiveDataLogging(dbOptions.EnableSensitiveDataLogging);
            });
        }
    }

    private static string BuildConnectionString(DatabaseSettings dbOptions, string moduleName)
    {
        var builder = new NpgsqlConnectionStringBuilder(dbOptions.ConnectionString)
        {
            MinPoolSize = dbOptions.ConnectionPool.MinPoolSize,
            MaxPoolSize = dbOptions.ConnectionPool.MaxPoolSize,
            ConnectionIdleLifetime = dbOptions.ConnectionPool.ConnectionIdleLifetime,
            Timeout = dbOptions.ConnectionPool.ConnectionTimeout,
            Multiplexing = false,
            TcpKeepAlive = true,
            TcpKeepAliveTime = 600,
            TcpKeepAliveInterval = 30,
            ApplicationName = moduleName,
            MaxAutoPrepare = 0
        };

        return builder.ConnectionString;
    }
}