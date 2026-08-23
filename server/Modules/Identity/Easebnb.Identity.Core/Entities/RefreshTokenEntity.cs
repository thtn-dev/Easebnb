using BuildingBlocks.SharedKernel.Common;

namespace Easebnb.Identity.Core.Entities;

public sealed class RefreshTokenEntity : IEntityBase<Guid>
{
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
    public Guid Id { get; set; }

    private bool IsExpired()
    {
        return DateTime.UtcNow >= ExpiresAt;
    }

    public bool IsActive()
    {
        return !IsRevoked && !IsExpired();
    }
}