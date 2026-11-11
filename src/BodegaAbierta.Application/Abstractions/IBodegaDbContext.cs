using BodegaAbierta.Domain.Common;
using BodegaAbierta.Domain.Donors;
using BodegaAbierta.Domain.FoodBanks;
using BodegaAbierta.Domain.Inventory;
using BodegaAbierta.Domain.Locations;
using BodegaAbierta.Domain.Products;
using Microsoft.EntityFrameworkCore;

namespace BodegaAbierta.Application.Abstractions;

/// <summary>
/// What the use cases need from persistence. Every tenant-owned DbSet is already filtered by the
/// current food bank (global query filter), so services never write <c>Where(x =&gt; x.FoodBankId == ...)</c>.
/// </summary>
public interface IBodegaDbContext
{
    DbSet<FoodBank> FoodBanks { get; }

    DbSet<StorageLocation> StorageLocations { get; }

    DbSet<Donor> Donors { get; }

    DbSet<Product> Products { get; }

    DbSet<DonationIntake> DonationIntakes { get; }

    DbSet<InventoryLot> InventoryLots { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Tells EF Core which version the client last saw. The UPDATE then includes
    /// <c>WHERE RowVersion = @expected</c>; if someone else saved first, 0 rows match and
    /// <see cref="DbUpdateConcurrencyException"/> is thrown.
    /// </summary>
    void SetExpectedRowVersion<TEntity>(TEntity entity, byte[] expectedRowVersion)
        where TEntity : class, IVersioned;
}
