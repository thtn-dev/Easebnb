using BuildingBlocks.SharedKernel.Common;

namespace TmsBase.Identity.Domain.Entities;

public sealed class RefreshTokenEntity : IEntityBase<Guid>
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Token { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool IsRevoked { get; set; }
    public string? RevokedByIp { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string? ReplacedByToken { get; set; }
    public string CreatedByIp { get; set; } = string.Empty;

    public User User { get; set; } = null!;

    private bool IsExpired() => DateTime.UtcNow >= ExpiresAt;
    public bool IsActive() => !IsRevoked && !IsExpired();
}