using BodegaAbierta.Domain.Donors;
using BodegaAbierta.Domain.FoodBanks;
using BodegaAbierta.Domain.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BodegaAbierta.Infrastructure.Persistence.Configurations;

internal sealed class DonationIntakeConfiguration : IEntityTypeConfiguration<DonationIntake>
{
    public void Configure(EntityTypeBuilder<DonationIntake> builder)
    {
        builder.ToTable("DonationIntakes");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.ReceivedBy).HasMaxLength(DonationIntake.ReceivedByMaxLength).IsRequired();
        builder.Property(x => x.Notes).HasMaxLength(DonationIntake.NotesMaxLength);

        builder.HasOne<FoodBank>().WithMany().HasForeignKey(x => x.FoodBankId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Donor>().WithMany().HasForeignKey(x => x.DonorId).OnDelete(DeleteBehavior.Restrict);

        // Lots are reached through the read-only Lots property; EF writes the private _lots field.
        builder.HasMany(x => x.Lots).WithOne().HasForeignKey(l => l.DonationIntakeId).OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(x => x.Lots).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(x => new { x.FoodBankId, x.ReceivedAt });
    }
}
