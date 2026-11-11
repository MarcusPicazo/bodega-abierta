using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using BodegaAbierta.Api.Tenancy;
using BodegaAbierta.Domain.FoodBanks;
using BodegaAbierta.Domain.Locations;

namespace BodegaAbierta.IntegrationTests.Infrastructure;

/// <summary>A food bank seeded for one test, plus an HttpClient that sends its X-FoodBank-Id.</summary>
public sealed record TestTenant(Guid FoodBankId, Guid StorageLocationId, HttpClient Client);

/// <summary>Base for HTTP-level tests: one in-memory API per test, talking to the Testcontainers SQL Server.</summary>
public abstract class ApiTestBase(SqlServerFixture sql) : IAsyncLifetime
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    protected BodegaApiFactory Factory { get; } = new(sql);

    protected SqlServerFixture Sql { get; } = sql;

    protected static CancellationToken Ct => TestContext.Current.CancellationToken;

    protected async Task<TestTenant> CreateTenantAsync()
    {
        var foodBank = new FoodBank($"Banco {Guid.NewGuid():N}");
        var location = new StorageLocation(foodBank.Id, "A-01", "Anaquel seco 1", StorageType.Dry);

        await using (var db = Sql.CreateDbContext(foodBankId: null))
        {
            db.FoodBanks.Add(foodBank);
            db.StorageLocations.Add(location);
            await db.SaveChangesAsync(Ct);
        }

        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Add(HttpTenantContext.HeaderName, foodBank.Id.ToString());
        return new TestTenant(foodBank.Id, location.Id, client);
    }

    protected static async Task<T> ReadAsync<T>(HttpResponseMessage response)
        => (await response.Content.ReadFromJsonAsync<T>(Json, Ct))!;

    protected static Task<HttpResponseMessage> PostAsync<T>(HttpClient client, string url, T body)
        => client.PostAsJsonAsync(url, body, Json, Ct);

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public async ValueTask DisposeAsync()
    {
        await Factory.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}
