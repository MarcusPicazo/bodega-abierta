using BodegaAbierta.Domain.Auditing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BodegaAbierta.Infrastructure.Persistence.Configurations;

internal sealed class AuditEntryConfiguration : IEntityTypeConfiguration<AuditEntry>
{
    public void Configure(EntityTypeBuilder<AuditEntry> builder)
    {
        // Append-only log: no foreign keys, so audit rows never block (or get removed by) other deletes.
        builder.ToTable("AuditEntries");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.EntityType).HasMaxLength(AuditEntry.EntityTypeMaxLength).IsRequired();
        builder.Property(x => x.EntityId).HasMaxLength(AuditEntry.EntityIdMaxLength).IsRequired();
        builder.Property(x => x.Action).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Actor).HasMaxLength(AuditEntry.ActorMaxLength).IsRequired();

        // "History of entity X" is the main query.
        builder.HasIndex(x => new { x.FoodBankId, x.EntityType, x.EntityId, x.Timestamp });
    }
}
