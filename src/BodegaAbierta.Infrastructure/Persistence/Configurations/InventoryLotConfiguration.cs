using BodegaAbierta.Domain.FoodBanks;
using BodegaAbierta.Domain.Inventory;
using BodegaAbierta.Domain.Locations;
using BodegaAbierta.Domain.Products;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BodegaAbierta.Infrastructure.Persistence.Configurations;

internal sealed class InventoryLotConfiguration : IEntityTypeConfiguration<InventoryLot>
{
    public void Configure(EntityTypeBuilder<InventoryLot> builder)
    {
        builder.ToTable("InventoryLots");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Unit).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.InitialQuantity).HasPrecision(18, 3);
        builder.Property(x => x.QuantityOnHand).HasPrecision(18, 3);

        builder.HasOne<FoodBank>().WithMany().HasForeignKey(x => x.FoodBankId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Product>().WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<StorageLocation>().WithMany().HasForeignKey(x => x.StorageLocationId).OnDelete(DeleteBehavior.Restrict);

        builder.ToTable(t => t.HasCheckConstraint(
            "CK_InventoryLots_Quantities",
            "[InitialQuantity] > 0 AND [QuantityOnHand] >= 0 AND [QuantityOnHand] <= [InitialQuantity]"));

        // FEFO lookup (Phase 4): "available lots of product X, earliest expiry first", covering quantity.
        builder.HasIndex(x => new { x.FoodBankId, x.ProductId, x.Status, x.ExpiresOn })
            .IncludeProperties(x => x.QuantityOnHand);
    }
}
