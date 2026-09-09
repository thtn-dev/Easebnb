using BuildingBlocks.SharedKernel.Common;

namespace Easebnb.Organization.Core.Entities;

public enum OrganizationStatus
{
    Active,
    Suspended,
    Archived
}

/// <summary>
///     A customer's business account (e.g. a hotel owner's company) that will
///     own properties, listings and other resources in later modules.
///     The owner is denormalized as <see cref="OwnerUserId" /> (always set,
///     kept in sync with the Owner membership row) so "who owns this
///     organization" never needs a join and is guaranteed at the database
///     level by a NOT NULL column.
/// </summary>
public sealed class Organization : IEntityBase<Guid>, IAggregateRoot, IAuditableEntity
{
    private Organization()
    {
    }

    private Organization(string name, string slug, string? description, Guid ownerUserId)
    {
        Id = Guid.CreateVersion7(DateTimeOffset.UtcNow);
        Name = name;
        Slug = slug;
        Description = description;
        OwnerUserId = ownerUserId;
        Status = OrganizationStatus.Active;
    }

    public Guid Id { get; set; }

    public string Name { get; private set; } = null!;

    public string Slug { get; private set; } = null!;

    public string? Description { get; private set; }

    /// <summary>S3 object key of the logo (no URL is persisted anywhere).</summary>
    public string? LogoKey { get; private set; }

    public OrganizationStatus Status { get; private set; }

    /// <summary>Logical reference to the Identity module's user (no FK across module schemas).</summary>
    public Guid OwnerUserId { get; private set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public bool IsActive => Status == OrganizationStatus.Active;

    public static Organization Create(string name, string slug, string? description, Guid ownerUserId)
    {
        return new Organization(name, slug, description, ownerUserId);
    }

    public void UpdateDetails(string name, string slug, string? description)
    {
        Name = name;
        Slug = slug;
        Description = description;
    }

    public void ChangeOwner(Guid newOwnerUserId)
    {
        OwnerUserId = newOwnerUserId;
    }

    public void SetLogo(string logoKey)
    {
        LogoKey = logoKey;
    }

    public void Archive()
    {
        Status = OrganizationStatus.Archived;
    }
}
