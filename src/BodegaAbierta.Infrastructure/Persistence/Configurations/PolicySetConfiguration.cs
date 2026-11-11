using BodegaAbierta.Domain.FoodBanks;
using BodegaAbierta.Domain.Policies;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BodegaAbierta.Infrastructure.Persistence.Configurations;

internal sealed class PolicySetConfiguration : IEntityTypeConfiguration<PolicySet>
{
    public void Configure(EntityTypeBuilder<PolicySet> builder)
    {
        builder.ToTable("PolicySets");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Description).HasMaxLength(PolicySet.DescriptionMaxLength);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.CreatedBy).HasMaxLength(PolicySet.ActorMaxLength).IsRequired();
        builder.Property(x => x.SubmittedBy).HasMaxLength(PolicySet.ActorMaxLength);
        builder.Property(x => x.ApprovedBy).HasMaxLength(PolicySet.ActorMaxLength);
        builder.Property(x => x.RejectionReason).HasMaxLength(PolicySet.ReasonMaxLength);

        builder.HasOne<FoodBank>().WithMany().HasForeignKey(x => x.FoodBankId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PolicySet>().WithMany().HasForeignKey(x => x.ReplacedByPolicySetId).OnDelete(DeleteBehavior.Restrict);

        // Rules belong to their set: removing one from the collection deletes the row.
        builder.HasMany(x => x.Rules).WithOne().HasForeignKey(r => r.PolicySetId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.Rules).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(x => new { x.FoodBankId, x.Version }).IsUnique();

        // The database-level guarantee: at most ONE active version per food bank, even if two
        // coordinators approve different versions at the same instant.
        builder.HasIndex(x => x.FoodBankId)
            .IsUnique()
            .HasFilter($"[Status] = '{nameof(PolicySetStatus.Active)}'")
            .HasDatabaseName("UX_PolicySets_OneActivePerFoodBank");
    }
}
