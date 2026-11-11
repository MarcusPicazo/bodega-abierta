using BodegaAbierta.Domain.FoodBanks;
using BodegaAbierta.Domain.Locations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BodegaAbierta.Infrastructure.Persistence.Configurations;

internal sealed class StorageLocationConfiguration : IEntityTypeConfiguration<StorageLocation>
{
    public void Configure(EntityTypeBuilder<StorageLocation> builder)
    {
        builder.ToTable("StorageLocations");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Code).HasMaxLength(StorageLocation.CodeMaxLength).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(StorageLocation.NameMaxLength).IsRequired();
        builder.Property(x => x.StorageType).HasConversion<string>().HasMaxLength(20);

        builder.HasOne<FoodBank>()
            .WithMany()
            .HasForeignKey(x => x.FoodBankId)
            .OnDelete(DeleteBehavior.Restrict);

        // FoodBankId leads the index: every query is tenant-scoped, so the filter column goes first.
        builder.HasIndex(x => new { x.FoodBankId, x.Code }).IsUnique();
    }
}
