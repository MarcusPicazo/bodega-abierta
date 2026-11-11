using System.Reflection;
using BodegaAbierta.Application.Abstractions;
using BodegaAbierta.Domain.Common;
using BodegaAbierta.Domain.FoodBanks;
using BodegaAbierta.Domain.Locations;
using Microsoft.EntityFrameworkCore;

namespace BodegaAbierta.Infrastructure.Persistence;

public sealed class BodegaAbiertaDbContext(
    DbContextOptions<BodegaAbiertaDbContext> options,
    ITenantContext tenantContext) : DbContext(options)
{
    /// <summary>Name of the tenant query filter; use <c>IgnoreQueryFilters([TenantFilter])</c> to bypass it on purpose.</summary>
    public const string TenantFilter = "Tenant";

    private static readonly MethodInfo ApplyTenantFilterMethod =
        typeof(BodegaAbiertaDbContext).GetMethod(nameof(ApplyTenantFilter), BindingFlags.NonPublic | BindingFlags.Instance)!;

    public DbSet<FoodBank> FoodBanks => Set<FoodBank>();

    public DbSet<StorageLocation> StorageLocations => Set<StorageLocation>();

    /// <summary>
    /// Read by the query filter on every query. Because it is a member of the context (not a captured
    /// constant), EF Core turns it into a SQL parameter, so one cached model serves every tenant.
    /// </summary>
    internal Guid? CurrentFoodBankId => tenantContext.FoodBankId;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BodegaAbiertaDbContext).Assembly);

        // Every root entity type implementing ITenantOwned gets the tenant filter automatically,
        // so a new entity cannot "forget" it.
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (entityType.BaseType is null && typeof(ITenantOwned).IsAssignableFrom(entityType.ClrType))
            {
                ApplyTenantFilterMethod.MakeGenericMethod(entityType.ClrType).Invoke(this, [modelBuilder]);
            }
        }
    }

    // TODO(Marcus): override SaveChangesAsync(CancellationToken) and, before calling base, walk
    // ChangeTracker.Entries<ITenantOwned>() in state Added or Modified. If an entry's FoodBankId differs
    // from CurrentFoodBankId (or there is no current tenant), throw InvalidOperationException.
    // Query filters only protect READS; this guard protects WRITES. Remove the Skip from
    // TenantIsolationTests.Saving_a_row_for_another_tenant_throws to check your work.

    private void ApplyTenantFilter<TEntity>(ModelBuilder modelBuilder)
        where TEntity : class, ITenantOwned
        => modelBuilder.Entity<TEntity>()
            .HasQueryFilter(TenantFilter, entity => entity.FoodBankId == CurrentFoodBankId);
}
