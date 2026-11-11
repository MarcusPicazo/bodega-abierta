using BodegaAbierta.Domain.FoodBanks;
using BodegaAbierta.Domain.Locations;
using BodegaAbierta.Infrastructure.Persistence;
using BodegaAbierta.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BodegaAbierta.IntegrationTests;

/// <summary>Proves the global query filter: tenant A can never read tenant B's rows.</summary>
public sealed class TenantIsolationTests(SqlServerFixture sql)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<(FoodBank A, FoodBank B)> SeedTwoFoodBanksAsync()
    {
        var a = new FoodBank("Banco de Alimentos A");
        var b = new FoodBank("Banco de Alimentos B");

        // Seeding is an admin operation: no tenant, filters don't apply to inserts.
        await using var db = sql.CreateDbContext(foodBankId: null);
        db.FoodBanks.AddRange(a, b);
        db.StorageLocations.AddRange(
            new StorageLocation(a.Id, "A-01", "Anaquel seco 1", StorageType.Dry),
            new StorageLocation(a.Id, "C-01", "Cámara fría", StorageType.Refrigerated),
            new StorageLocation(b.Id, "B-01", "Congelador", StorageType.Frozen));
        await db.SaveChangesAsync(Ct);

        return (a, b);
    }

    [Fact]
    public async Task Tenant_only_sees_its_own_rows()
    {
        var (a, b) = await SeedTwoFoodBanksAsync();

        await using var dbA = sql.CreateDbContext(a.Id);
        await using var dbB = sql.CreateDbContext(b.Id);

        var locationsA = await dbA.StorageLocations.ToListAsync(Ct);
        var locationsB = await dbB.StorageLocations.ToListAsync(Ct);

        locationsA.Should().HaveCount(2).And.OnlyContain(l => l.FoodBankId == a.Id);
        locationsB.Should().ContainSingle().Which.FoodBankId.Should().Be(b.Id);
    }

    [Fact]
    public async Task Tenant_cannot_read_another_tenants_row_even_by_id()
    {
        var (a, b) = await SeedTwoFoodBanksAsync();
        await using var dbB = sql.CreateDbContext(b.Id);
        var rowOfB = await dbB.StorageLocations.SingleAsync(Ct);

        await using var dbA = sql.CreateDbContext(a.Id);
        var leaked = await dbA.StorageLocations.FirstOrDefaultAsync(l => l.Id == rowOfB.Id, Ct);

        leaked.Should().BeNull();
    }

    [Fact]
    public async Task No_tenant_means_no_rows_fail_closed()
    {
        await SeedTwoFoodBanksAsync();

        await using var db = sql.CreateDbContext(foodBankId: null);

        (await db.StorageLocations.AnyAsync(Ct)).Should().BeFalse();
    }

    [Fact]
    public async Task Filter_is_a_sql_parameter_not_a_constant()
    {
        await using var db = sql.CreateDbContext(Guid.NewGuid());

        var sqlText = db.StorageLocations.ToQueryString();

        sqlText.Should().Contain("[FoodBankId] = @");
    }

    [Fact]
    public async Task IgnoreQueryFilters_is_an_explicit_escape_hatch()
    {
        var (a, b) = await SeedTwoFoodBanksAsync();

        await using var db = sql.CreateDbContext(a.Id);
        var all = await db.StorageLocations
            .IgnoreQueryFilters([BodegaAbiertaDbContext.TenantFilter])
            .Where(l => l.FoodBankId == a.Id || l.FoodBankId == b.Id)
            .ToListAsync(Ct);

        all.Should().HaveCount(3);
    }

    [Fact(Skip = "TODO(Marcus): implement the write guard in BodegaAbiertaDbContext.SaveChangesAsync")]
    public async Task Saving_a_row_for_another_tenant_throws()
    {
        var (a, b) = await SeedTwoFoodBanksAsync();

        await using var dbA = sql.CreateDbContext(a.Id);
        dbA.StorageLocations.Add(new StorageLocation(b.Id, "X-99", "Intruso", StorageType.Dry));

        var act = () => dbA.SaveChangesAsync(Ct);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
