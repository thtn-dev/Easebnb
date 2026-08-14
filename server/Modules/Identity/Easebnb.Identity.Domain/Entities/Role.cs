using BuildingBlocks.SharedKernel.Common;
using Microsoft.AspNetCore.Identity;

namespace TmsBase.Identity.Domain.Entities;

public class Role : IdentityRole<Guid>, IEntityBase<Guid>, IAuditableEntity
{
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}