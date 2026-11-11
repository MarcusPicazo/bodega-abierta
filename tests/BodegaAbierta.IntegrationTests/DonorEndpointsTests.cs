using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using BodegaAbierta.Application.Common;
using BodegaAbierta.Application.Donors;
using BodegaAbierta.Domain.Donors;
using BodegaAbierta.IntegrationTests.Infrastructure;

namespace BodegaAbierta.IntegrationTests;

public sealed class DonorEndpointsTests(SqlServerFixture sql) : ApiTestBase(sql)
{
    private const string Url = "/api/v1/donors";

    private static CreateDonorRequest NewDonor(string name = "Supermercado La Esperanza")
        => new(name, DonorType.Business, "donaciones@example.org", "55 1234 5678");

    private static UpdateDonorRequest Rename(string name) => new(name, DonorType.Business, null, null, IsActive: true);

    private static Task<HttpResponseMessage> PutAsync(HttpClient client, Guid id, UpdateDonorRequest body, string? ifMatch)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, $"{Url}/{id}") { Content = JsonContent.Create(body, options: Json) };
        if (ifMatch is not null)
        {
            request.Headers.IfMatch.Add(EntityTagHeaderValue.Parse(ifMatch));
        }

        return client.SendAsync(request, Ct);
    }

    [Fact]
    public async Task Create_returns_201_with_location_and_etag()
    {
        var tenant = await CreateTenantAsync();

        var response = await PostAsync(tenant.Client, Url, NewDonor());

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var donor = await ReadAsync<DonorResponse>(response);
        response.Headers.Location!.ToString().Should().Be($"{Url}/{donor.Id}");
        response.Headers.ETag!.Tag.Should().Be($"\"{donor.Version}\"");
        donor.IsActive.Should().BeTrue();

        var fetched = await tenant.Client.GetFromJsonAsync<DonorResponse>($"{Url}/{donor.Id}", Json, Ct);
        fetched.Should().BeEquivalentTo(donor);
    }

    [Fact]
    public async Task Invalid_body_returns_400_validation_problem_with_camelCase_keys()
    {
        var tenant = await CreateTenantAsync();

        var response = await PostAsync(tenant.Client, Url, new CreateDonorRequest("", DonorType.Business, "not-an-email", null));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        var errors = body.RootElement.GetProperty("errors");
        errors.TryGetProperty("name", out _).Should().BeTrue();
        errors.TryGetProperty("email", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Missing_tenant_header_returns_400()
    {
        using var client = Factory.CreateClient();

        var response = await client.GetAsync(new Uri(Url, UriKind.Relative), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync(Ct)).Should().Contain("X-FoodBank-Id");
    }

    [Fact]
    public async Task Another_food_bank_gets_404_for_my_donor()
    {
        var a = await CreateTenantAsync();
        var b = await CreateTenantAsync();
        var donorOfA = await ReadAsync<DonorResponse>(await PostAsync(a.Client, Url, NewDonor()));

        var response = await b.Client.GetAsync(new Uri($"{Url}/{donorOfA.Id}", UriKind.Relative), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Update_with_current_etag_succeeds_and_returns_a_new_etag()
    {
        var tenant = await CreateTenantAsync();
        var created = await ReadAsync<DonorResponse>(await PostAsync(tenant.Client, Url, NewDonor()));

        var response = await PutAsync(tenant.Client, created.Id, Rename("La Esperanza Sucursal Centro"), $"\"{created.Version}\"");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await ReadAsync<DonorResponse>(response);
        updated.Name.Should().Be("La Esperanza Sucursal Centro");
        updated.Version.Should().NotBe(created.Version);
        response.Headers.ETag!.Tag.Should().Be($"\"{updated.Version}\"");
    }

    [Fact]
    public async Task Update_with_stale_etag_returns_412_and_keeps_the_first_write()
    {
        var tenant = await CreateTenantAsync();
        var created = await ReadAsync<DonorResponse>(await PostAsync(tenant.Client, Url, NewDonor()));
        var staleEtag = $"\"{created.Version}\"";

        // Two coordinators loaded the same version; the first one saves...
        (await PutAsync(tenant.Client, created.Id, Rename("Primero"), staleEtag)).StatusCode.Should().Be(HttpStatusCode.OK);

        // ...and the second one, still holding the old version, is rejected instead of overwriting.
        var second = await PutAsync(tenant.Client, created.Id, Rename("Segundo"), staleEtag);

        second.StatusCode.Should().Be(HttpStatusCode.PreconditionFailed);
        var current = await tenant.Client.GetFromJsonAsync<DonorResponse>($"{Url}/{created.Id}", Json, Ct);
        current!.Name.Should().Be("Primero");
    }

    [Fact]
    public async Task Update_without_if_match_returns_428()
    {
        var tenant = await CreateTenantAsync();
        var created = await ReadAsync<DonorResponse>(await PostAsync(tenant.Client, Url, NewDonor()));

        var response = await PutAsync(tenant.Client, created.Id, Rename("Sin versión"), ifMatch: null);

        response.StatusCode.Should().Be(HttpStatusCode.PreconditionRequired);
    }

    [Fact]
    public async Task List_is_paged_and_sorted_by_name()
    {
        var tenant = await CreateTenantAsync();
        foreach (var name in new[] { "Carla", "Abarrotes Benito", "Banco Comunitario" })
        {
            await PostAsync(tenant.Client, Url, NewDonor(name));
        }

        var page = await tenant.Client.GetFromJsonAsync<PagedResult<DonorResponse>>($"{Url}?page=1&pageSize=2", Json, Ct);

        page!.TotalCount.Should().Be(3);
        page.Items.Select(d => d.Name).Should().Equal("Abarrotes Benito", "Banco Comunitario");
    }

    [Fact(Skip = "TODO(Marcus): implement the Search filter in DonorService.ListAsync")]
    public async Task List_filters_by_search()
    {
        var tenant = await CreateTenantAsync();
        await PostAsync(tenant.Client, Url, NewDonor("Panadería San Juan"));
        await PostAsync(tenant.Client, Url, NewDonor("Tortillería Lupita"));

        var page = await tenant.Client.GetFromJsonAsync<PagedResult<DonorResponse>>($"{Url}?search=%20panader%20", Json, Ct);

        page!.Items.Should().ContainSingle().Which.Name.Should().Be("Panadería San Juan");
    }
}
