using System.Net;
using BodegaAbierta.Application.Products;
using BodegaAbierta.Domain.Products;
using BodegaAbierta.IntegrationTests.Infrastructure;

namespace BodegaAbierta.IntegrationTests;

public sealed class ProductEndpointsTests(SqlServerFixture sql) : ApiTestBase(sql)
{
    private const string Url = "/api/v1/products";

    private static CreateProductRequest Beans(string name = "Frijol negro")
        => new(name, ProductCategory.Grains, UnitOfMeasure.Kilogram, IsPerishable: false);

    [Fact]
    public async Task Create_returns_201_with_enums_as_strings()
    {
        var tenant = await CreateTenantAsync();

        var response = await PostAsync(tenant.Client, Url, Beans());

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var json = await response.Content.ReadAsStringAsync(Ct);
        json.Should().Contain("\"category\":\"Grains\"").And.Contain("\"unit\":\"Kilogram\"");
    }

    [Fact]
    public async Task Duplicate_name_in_the_same_food_bank_returns_409()
    {
        var tenant = await CreateTenantAsync();
        await PostAsync(tenant.Client, Url, Beans("Frijol negro"));

        var response = await PostAsync(tenant.Client, Url, Beans("FRIJOL NEGRO"));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Same_name_in_different_food_banks_is_allowed()
    {
        var a = await CreateTenantAsync();
        var b = await CreateTenantAsync();

        (await PostAsync(a.Client, Url, Beans())).StatusCode.Should().Be(HttpStatusCode.Created);
        (await PostAsync(b.Client, Url, Beans())).StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Unknown_enum_value_returns_400()
    {
        var tenant = await CreateTenantAsync();

        var response = await PostAsync(tenant.Client, Url, new { name = "Algo", category = "Spaceship", unit = "Kilogram", isPerishable = false });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
