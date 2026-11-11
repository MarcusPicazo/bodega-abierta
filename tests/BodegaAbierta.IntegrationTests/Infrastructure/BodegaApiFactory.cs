using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace BodegaAbierta.IntegrationTests.Infrastructure;

/// <summary>Hosts the real API in memory, pointed at the Testcontainers SQL Server.</summary>
public sealed class BodegaApiFactory(SqlServerFixture sql) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:BodegaAbierta", sql.ConnectionString);
    }
}
