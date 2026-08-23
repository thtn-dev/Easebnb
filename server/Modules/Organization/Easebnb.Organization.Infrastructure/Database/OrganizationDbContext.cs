using Easebnb.Database.Extensions;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Easebnb.Organization.Infrastructure.Database;

public class OrganizationDbContext(DbContextOptions<OrganizationDbContext> options)
    : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("organization");

        modelBuilder.ApplyAuditableConventions();

        // MassTransit transactional outbox/inbox state (consumer side), kept
        // inside this module's own schema.
        modelBuilder.AddTransactionalOutboxEntities();
    }
}
