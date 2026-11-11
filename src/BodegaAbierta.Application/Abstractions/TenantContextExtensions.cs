namespace BodegaAbierta.Application.Abstractions;

public static class TenantContextExtensions
{
    /// <summary>Returns the current food bank or throws <see cref="TenantNotResolvedException"/> (HTTP 400).</summary>
    public static Guid RequireFoodBankId(this ITenantContext tenantContext)
    {
        ArgumentNullException.ThrowIfNull(tenantContext);
        return tenantContext.FoodBankId ?? throw new TenantNotResolvedException();
    }
}

public sealed class TenantNotResolvedException : Exception
{
    public TenantNotResolvedException()
        : base("The food bank could not be resolved for this request. Send the X-FoodBank-Id header.") { }

    public TenantNotResolvedException(string message) : base(message) { }

    public TenantNotResolvedException(string message, Exception innerException) : base(message, innerException) { }
}
