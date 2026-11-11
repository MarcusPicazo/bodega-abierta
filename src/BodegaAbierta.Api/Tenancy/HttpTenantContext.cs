using BodegaAbierta.Application.Abstractions;

namespace BodegaAbierta.Api.Tenancy;

/// <summary>Resolves the current food bank from the incoming HTTP request.</summary>
internal sealed class HttpTenantContext(IHttpContextAccessor httpContextAccessor) : ITenantContext
{
    public const string HeaderName = "X-FoodBank-Id";
    public const string ClaimType = "food_bank_id";

    public Guid? FoodBankId => Resolve(httpContextAccessor.HttpContext);

    private static Guid? Resolve(HttpContext? httpContext)
    {
        if (httpContext is null)
        {
            return null;
        }

        // TODO(Marcus): once authentication exists the claim must win over the header. Read
        // httpContext.User.FindFirst(ClaimType); if it holds a valid, non-empty Guid return it and
        // ignore the header. Add a test in HttpTenantContextTests proving "claim beats header".

        return httpContext.Request.Headers.TryGetValue(HeaderName, out var values)
            && Guid.TryParse(values.ToString(), out var foodBankId)
            && foodBankId != Guid.Empty
                ? foodBankId
                : null;
    }
}
