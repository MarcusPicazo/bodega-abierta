using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BodegaAbierta.Application.Common;
using BodegaAbierta.Application.Donors;
using BodegaAbierta.Application.Intakes;
using BodegaAbierta.Application.Inventory;
using BodegaAbierta.Application.Products;
using BodegaAbierta.Domain.Donors;
using BodegaAbierta.Domain.Inventory;
using BodegaAbierta.Domain.Products;
using BodegaAbierta.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BodegaAbierta.IntegrationTests;

public sealed class DonationIntakeEndpointsTests(SqlServerFixture sql) : ApiTestBase(sql)
{
    private const string Url = "/api/v1/donation-intakes";

    private static readonly DateTimeOffset ReceivedAt = new(2026, 11, 10, 9, 0, 0, TimeSpan.FromHours(-6));

    private static async Task<(Guid DonorId, Guid MilkId, Guid BeansId)> SeedCatalogAsync(TestTenant tenant)
    {
        var donor = await ReadAsync<DonorResponse>(await PostAsync(tenant.Client, "/api/v1/donors",
            new CreateDonorRequest("Supermercado La Esperanza", DonorType.Business, null, null)));
        var milk = await ReadAsync<ProductResponse>(await PostAsync(tenant.Client, "/api/v1/products",
            new CreateProductRequest("Leche entera 1 L", ProductCategory.Dairy, UnitOfMeasure.Unit, IsPerishable: true)));
        var beans = await ReadAsync<ProductResponse>(await PostAsync(tenant.Client, "/api/v1/products",
            new CreateProductRequest("Frijol negro", ProductCategory.Grains, UnitOfMeasure.Kilogram, IsPerishable: false)));

        return (donor.Id, milk.Id, beans.Id);
    }

    [Fact]
    public async Task Intake_creates_one_lot_per_line_listed_in_fefo_order()
    {
        var tenant = await CreateTenantAsync();
        var (donorId, milkId, beansId) = await SeedCatalogAsync(tenant);

        var response = await PostAsync(tenant.Client, Url, new CreateDonationIntakeRequest(donorId, "Ana (almacén)", ReceivedAt, "Entrega semanal",
        [
            new(beansId, tenant.StorageLocationId, 50m, null),
            new(milkId, tenant.StorageLocationId, 24m, new DateOnly(2026, 11, 25)),
            new(milkId, tenant.StorageLocationId, 12m, new DateOnly(2026, 11, 15)),
        ]));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var intake = await ReadAsync<DonationIntakeResponse>(response);
        intake.Lots.Should().HaveCount(3).And.OnlyContain(l => l.Status == LotStatus.Available && l.QuantityOnHand == l.InitialQuantity);
        intake.Lots.Single(l => l.ProductId == beansId).Unit.Should().Be(UnitOfMeasure.Kilogram);

        var lots = await tenant.Client.GetFromJsonAsync<PagedResult<InventoryLotResponse>>("/api/v1/inventory-lots", Json, Ct);
        lots!.Items.Select(l => l.ExpiresOn).Should().Equal(new DateOnly(2026, 11, 15), new DateOnly(2026, 11, 25), null);

        var expiringSoon = await tenant.Client.GetFromJsonAsync<PagedResult<InventoryLotResponse>>(
            "/api/v1/inventory-lots?expiringOnOrBefore=2026-11-20", Json, Ct);
        expiringSoon!.Items.Should().ContainSingle().Which.QuantityOnHand.Should().Be(12m);

        var fetched = await tenant.Client.GetFromJsonAsync<DonationIntakeResponse>($"{Url}/{intake.Id}", Json, Ct);
        fetched!.Lots.Should().HaveCount(3);
    }

    [Fact]
    public async Task Perishable_without_expiry_returns_422_and_saves_nothing()
    {
        var tenant = await CreateTenantAsync();
        var (donorId, milkId, beansId) = await SeedCatalogAsync(tenant);

        var response = await PostAsync(tenant.Client, Url, new CreateDonationIntakeRequest(donorId, "Ana", ReceivedAt, null,
        [
            new(beansId, tenant.StorageLocationId, 50m, null),
            new(milkId, tenant.StorageLocationId, 24m, null),
        ]));

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        body.RootElement.GetProperty("code").GetString().Should().Be("lot.expiry_required");

        await using var db = Sql.CreateDbContext(tenant.FoodBankId);
        (await db.InventoryLots.AnyAsync(Ct)).Should().BeFalse("the intake is all or nothing");
        (await db.DonationIntakes.AnyAsync(Ct)).Should().BeFalse();
    }

    [Fact]
    public async Task Inactive_donor_returns_422()
    {
        var tenant = await CreateTenantAsync();
        var (donorId, _, beansId) = await SeedCatalogAsync(tenant);
        var donor = await tenant.Client.GetAsync(new Uri($"/api/v1/donors/{donorId}", UriKind.Relative), Ct);
        var deactivate = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/donors/{donorId}")
        {
            Content = JsonContent.Create(new UpdateDonorRequest("Supermercado La Esperanza", DonorType.Business, null, null, IsActive: false), options: Json),
        };
        deactivate.Headers.IfMatch.Add(donor.Headers.ETag!);
        (await tenant.Client.SendAsync(deactivate, Ct)).StatusCode.Should().Be(HttpStatusCode.OK);

        var response = await PostAsync(tenant.Client, Url, new CreateDonationIntakeRequest(donorId, "Ana", ReceivedAt, null,
            [new(beansId, tenant.StorageLocationId, 5m, null)]));

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await response.Content.ReadAsStringAsync(Ct)).Should().Contain("donor.inactive");
    }

    [Fact]
    public async Task Product_of_another_food_bank_is_reported_as_not_found_on_its_line()
    {
        var a = await CreateTenantAsync();
        var b = await CreateTenantAsync();
        var (donorOfA, _, _) = await SeedCatalogAsync(a);
        var (_, _, beansOfB) = await SeedCatalogAsync(b);

        var response = await PostAsync(a.Client, Url, new CreateDonationIntakeRequest(donorOfA, "Ana", ReceivedAt, null,
            [new(beansOfB, a.StorageLocationId, 5m, null)]));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        body.RootElement.GetProperty("errors").TryGetProperty("lines[0].productId", out _).Should().BeTrue();
    }
}
