using BodegaAbierta.Application.Abstractions;
using BodegaAbierta.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;

[assembly: AssemblyFixture(typeof(BodegaAbierta.IntegrationTests.Infrastructure.SqlServerFixture))]

namespace BodegaAbierta.IntegrationTests.Infrastructure;

/// <summary>
/// One throw-away SQL Server container for the whole test assembly (Testcontainers picks a free
/// host port, so it never clashes with the docker compose container on 1434).
/// </summary>
public sealed class SqlServerFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();

        await using var db = CreateDbContext(foodBankId: null);
        await db.Database.MigrateAsync();
    }

    public ValueTask DisposeAsync() => _container.DisposeAsync();

    /// <summary>A context bound to a fixed tenant, like one HTTP request would be.</summary>
    public BodegaAbiertaDbContext CreateDbContext(Guid? foodBankId)
    {
        var options = new DbContextOptionsBuilder<BodegaAbiertaDbContext>()
            .UseSqlServer(ConnectionString)
            .Options;

        return new BodegaAbiertaDbContext(options, new FixedTenantContext(foodBankId));
    }

    private sealed record FixedTenantContext(Guid? FoodBankId) : ITenantContext;
}
