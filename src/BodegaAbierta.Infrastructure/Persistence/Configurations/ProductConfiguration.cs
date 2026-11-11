using BodegaAbierta.Domain.FoodBanks;
using BodegaAbierta.Domain.Products;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BodegaAbierta.Infrastructure.Persistence.Configurations;

internal sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("Products");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Name).HasMaxLength(Product.NameMaxLength).IsRequired();
        builder.Property(x => x.Category).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.Unit).HasConversion<string>().HasMaxLength(20);

        builder.HasOne<FoodBank>().WithMany().HasForeignKey(x => x.FoodBankId).OnDelete(DeleteBehavior.Restrict);

        // One catalog entry per name per food bank (case-insensitive with the default collation).
        builder.HasIndex(x => new { x.FoodBankId, x.Name }).IsUnique();
    }
}
