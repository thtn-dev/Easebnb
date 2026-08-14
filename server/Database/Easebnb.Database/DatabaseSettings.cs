using System.ComponentModel.DataAnnotations;

namespace Easebnb.Database;

public class DatabaseSettings
{
    public const string SectionName = "Database";

    /// <summary>
    ///     PostgreSQL connection string with dynamic pooling parameters
    /// </summary>
    [Required]
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    ///     Enable sensitive data logging (set to false in production)
    /// </summary>
    public bool EnableSensitiveDataLogging { get; set; } = false;

    /// <summary>
    ///     Enable detailed error messages (helpful for development)
    /// </summary>
    public bool EnableDetailedErrors { get; set; } = true;

    /// <summary>
    ///     Connection pool settings for dynamic scaling
    /// </summary>
    public ConnectionPoolOptions ConnectionPool { get; set; } = new();
}

/// <summary>
///     Connection pool configuration for dynamic scaling
/// </summary>
public class ConnectionPoolOptions
{
    /// <summary>
    ///     Minimum number of connections to maintain in pool
    ///     These connections are kept warm for immediate use
    /// </summary>
    public int MinPoolSize { get; set; } = 25;

    /// <summary>
    ///     Maximum number of connections in pool
    ///     This prevents overwhelming the database
    /// </summary>
    public int MaxPoolSize { get; set; } = 100;

    /// <summary>
    ///     How long to wait for a connection before timing out
    /// </summary>
    public int ConnectionTimeout { get; set; } = 30;

    /// <summary>
    ///     How long a connection can be idle before being closed
    ///     This helps free up resources during low-traffic periods
    /// </summary>
    public int ConnectionIdleLifetime { get; set; } = 300; // 5 minutes

    /// <summary>
    ///     Maximum lifetime of a connection before it's refreshed
    ///     This prevents connection leaks and ensures fresh connections
    /// </summary>
    public int ConnectionLifetime { get; set; } = 3600; // 1 hour

    /// <summary>
    ///     Enable connection pooling health checks
    /// </summary>
    public bool EnableHealthChecks { get; set; } = true;

    /// <summary>
    ///     Interval for connection pool health checks (in seconds)
    /// </summary>
    public int HealthCheckInterval { get; set; } = 30;
}