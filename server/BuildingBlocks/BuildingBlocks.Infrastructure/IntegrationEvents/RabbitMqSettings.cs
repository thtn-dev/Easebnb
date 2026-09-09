using System.ComponentModel.DataAnnotations;

namespace BuildingBlocks.Infrastructure.IntegrationEvents;

public sealed class RabbitMqSettings
{
    public const string SectionName = "RabbitMq";

    /// <summary>
    ///     When false the bus falls back to the in-memory transport so the app
    ///     (and the integration test suite) runs without a broker. The outbox/
    ///     inbox pipeline stays active in both modes.
    /// </summary>
    public bool Enabled { get; init; }

    [Required] public string Host { get; init; } = "localhost";

    [Required] public int Port { get; init; } = 5672;

    [Required] public string Username { get; init; } = "guest";

    [Required] public string Password { get; init; } = "guest";

    [Required] public string VirtualHost { get; init; } = "/";
}
