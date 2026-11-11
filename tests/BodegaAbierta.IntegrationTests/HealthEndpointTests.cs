using System.Net;
using BodegaAbierta.IntegrationTests.Infrastructure;

namespace BodegaAbierta.IntegrationTests;

public sealed class HealthEndpointTests(SqlServerFixture sql) : IAsyncLifetime
{
    private readonly BodegaApiFactory _factory = new(sql);

    [Theory]
    [InlineData("/health")]
    [InlineData("/health/live")]
    public async Task Health_endpoints_report_healthy(string path)
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync(new Uri(path, UriKind.Relative), TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Should().Be("Healthy");
    }

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public ValueTask DisposeAsync() => _factory.DisposeAsync();
}
