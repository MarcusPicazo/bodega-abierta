using BodegaAbierta.Domain.Beneficiaries;
using BodegaAbierta.Domain.Distributions;
using BodegaAbierta.Domain.FoodBanks;
using BodegaAbierta.Domain.Inventory;
using BodegaAbierta.Domain.Policies;
using BodegaAbierta.Domain.Products;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BodegaAbierta.Infrastructure.Persistence.Configurations;

internal sealed class BeneficiaryConfiguration : IEntityTypeConfiguration<Beneficiary>
{
    public void Configure(EntityTypeBuilder<Beneficiary> builder)
    {
        builder.ToTable("Beneficiaries");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Name).HasMaxLength(Beneficiary.NameMaxLength).IsRequired();
        builder.Property(x => x.Type).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Phone).HasMaxLength(Beneficiary.PhoneMaxLength);

        builder.HasOne<FoodBank>().WithMany().HasForeignKey(x => x.FoodBankId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.FoodBankId, x.Name });
    }
}

internal sealed class PickupSlotConfiguration : IEntityTypeConfiguration<PickupSlot>
{
    public void Configure(EntityTypeBuilder<PickupSlot> builder)
    {
        builder.ToTable("PickupSlots", t =>
        {
            t.HasCheckConstraint("CK_PickupSlots_Window", "[EndsAt] > [StartsAt]");
            t.HasCheckConstraint("CK_PickupSlots_Capacity", "[Capacity] >= 1 AND [ReservedCount] >= 0 AND [ReservedCount] <= [Capacity]");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Ignore(x => x.IsFull);
        builder.Ignore(x => x.PickupDate);

        builder.HasOne<FoodBank>().WithMany().HasForeignKey(x => x.FoodBankId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.FoodBankId, x.StartsAt });
    }
}

internal sealed class DistributionConfiguration : IEntityTypeConfiguration<Distribution>
{
    public void Configure(EntityTypeBuilder<Distribution> builder)
    {
        builder.ToTable("Distributions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.CreatedBy).HasMaxLength(Distribution.ActorMaxLength).IsRequired();

        builder.HasOne<FoodBank>().WithMany().HasForeignKey(x => x.FoodBankId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Beneficiary>().WithMany().HasForeignKey(x => x.BeneficiaryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PickupSlot>().WithMany().HasForeignKey(x => x.PickupSlotId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PolicySet>().WithMany().HasForeignKey(x => x.PolicySetId).OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.Allocations).WithOne().HasForeignKey(a => a.DistributionId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.Allocations).UsePropertyAccessMode(PropertyAccessMode.Field);

        // BeneficiaryLimit: "kg for beneficiary X scheduled in (from, to]".
        builder.HasIndex(x => new { x.FoodBankId, x.BeneficiaryId, x.ScheduledFor });
    }
}

internal sealed class DistributionAllocationConfiguration : IEntityTypeConfiguration<DistributionAllocation>
{
    public void Configure(EntityTypeBuilder<DistributionAllocation> builder)
    {
        builder.ToTable("DistributionAllocations", t => t.HasCheckConstraint("CK_DistributionAllocations_Quantity", "[Quantity] > 0"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Unit).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Quantity).HasPrecision(18, 3);

        builder.HasOne<InventoryLot>().WithMany().HasForeignKey(x => x.InventoryLotId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Product>().WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.InventoryLotId);
    }
}
