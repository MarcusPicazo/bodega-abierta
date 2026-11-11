using BodegaAbierta.Api.Tenancy;
using Microsoft.AspNetCore.Http;
using NSubstitute;

namespace BodegaAbierta.UnitTests.Tenancy;

public sealed class HttpTenantContextTests
{
    private static HttpTenantContext CreateSut(HttpContext? httpContext)
    {
        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(httpContext);
        return new HttpTenantContext(accessor);
    }

    private static DefaultHttpContext WithHeader(string value)
    {
        var context = new DefaultHttpContext();
        context.Request.Headers[HttpTenantContext.HeaderName] = value;
        return context;
    }

    [Fact]
    public void Valid_header_resolves_the_food_bank()
    {
        var foodBankId = Guid.NewGuid();

        var sut = CreateSut(WithHeader(foodBankId.ToString()));

        sut.FoodBankId.Should().Be(foodBankId);
    }

    [Fact]
    public void Missing_header_resolves_no_tenant()
    {
        CreateSut(new DefaultHttpContext()).FoodBankId.Should().BeNull();
    }

    [Theory]
    [InlineData("not-a-guid")]
    [InlineData("")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public void Invalid_header_resolves_no_tenant(string headerValue)
    {
        CreateSut(WithHeader(headerValue)).FoodBankId.Should().BeNull();
    }

    [Fact]
    public void No_http_context_resolves_no_tenant()
    {
        CreateSut(httpContext: null).FoodBankId.Should().BeNull();
    }
}
