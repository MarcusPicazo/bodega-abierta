using System.Reflection;
using BodegaAbierta.Application.Abstractions;
using BodegaAbierta.Domain.Auditing;
using BodegaAbierta.Domain.Beneficiaries;
using BodegaAbierta.Domain.Distributions;
using BodegaAbierta.Domain.Common;
using BodegaAbierta.Domain.Policies;
using BodegaAbierta.Domain.Donors;
using BodegaAbierta.Domain.FoodBanks;
using BodegaAbierta.Domain.Inventory;
using BodegaAbierta.Domain.Locations;
using BodegaAbierta.Domain.Products;
using Microsoft.EntityFrameworkCore;

namespace BodegaAbierta.Infrastructure.Persistence;

public sealed class BodegaAbiertaDbContext(
    DbContextOptions<BodegaAbiertaDbContext> options,
    ITenantContext tenantContext) : DbContext(options), IBodegaDbContext
{
    /// <summary>Name of the tenant query filter; use <c>IgnoreQueryFilters([TenantFilter])</c> to bypass it on purpose.</summary>
    public const string TenantFilter = "Tenant";

    private static readonly MethodInfo ApplyTenantFilterMethod =
        typeof(BodegaAbiertaDbContext).GetMethod(nameof(ApplyTenantFilter), BindingFlags.NonPublic | BindingFlags.Instance)!;

    public DbSet<FoodBank> FoodBanks => Set<FoodBank>();

    public DbSet<StorageLocation> StorageLocations => Set<StorageLocation>();

    public DbSet<Donor> Donors => Set<Donor>();

    public DbSet<Product> Products => Set<Product>();

    public DbSet<DonationIntake> DonationIntakes => Set<DonationIntake>();

    public DbSet<InventoryLot> InventoryLots => Set<InventoryLot>();

    public DbSet<PolicySet> PolicySets => Set<PolicySet>();

    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

    public DbSet<Beneficiary> Beneficiaries => Set<Beneficiary>();

    public DbSet<PickupSlot> PickupSlots => Set<PickupSlot>();

    public DbSet<Distribution> Distributions => Set<Distribution>();

    /// <summary>
    /// Read by the query filter on every query. Because it is a member of the context (not a captured
    /// constant), EF Core turns it into a SQL parameter, so one cached model serves every tenant.
    /// </summary>
    internal Guid? CurrentFoodBankId => tenantContext.FoodBankId;

    public void SetExpectedRowVersion<TEntity>(TEntity entity, byte[] expectedRowVersion)
        where TEntity : class, IVersioned
        => Entry(entity).Property(e => e.RowVersion).OriginalValue = expectedRowVersion;

    public Task ExecuteInTransactionAsync(Func<CancellationToken, Task> work, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(work);

        // With EnableRetryOnFailure a user-initiated transaction must run inside the execution strategy,
        // so a transient failure retries the whole unit (BeginTransaction ... Commit) and not half of it.
        return Database.CreateExecutionStrategy().ExecuteAsync(async ct =>
        {
            await using var transaction = await Database.BeginTransactionAsync(ct);
            await work(ct);
            await transaction.CommitAsync(ct);
        }, cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BodegaAbiertaDbContext).Assembly);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var clrType = entityType.ClrType;

            // Every root entity type implementing ITenantOwned gets the tenant filter automatically,
            // so a new entity cannot "forget" it.
            if (entityType.BaseType is null && typeof(ITenantOwned).IsAssignableFrom(clrType))
            {
                ApplyTenantFilterMethod.MakeGenericMethod(clrType).Invoke(this, [modelBuilder]);
            }

            // Optimistic concurrency by convention: IVersioned.RowVersion -> SQL Server rowversion.
            if (typeof(IVersioned).IsAssignableFrom(clrType))
            {
                modelBuilder.Entity(clrType).Property(nameof(IVersioned.RowVersion)).IsRowVersion();
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
