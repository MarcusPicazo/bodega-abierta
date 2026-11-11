using BodegaAbierta.Domain.FoodBanks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BodegaAbierta.Infrastructure.Persistence.Configurations;

internal sealed class FoodBankConfiguration : IEntityTypeConfiguration<FoodBank>
{
    public void Configure(EntityTypeBuilder<FoodBank> builder)
    {
        builder.ToTable("FoodBanks");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Name).HasMaxLength(FoodBank.NameMaxLength).IsRequired();
    }
}
