namespace BodegaAbierta.Application.Abstractions;

/// <summary>
/// The food bank (tenant) the current operation runs for.
/// Scoped: one value per HTTP request / unit of work.
/// </summary>
public interface ITenantContext
{
    /// <summary>The current tenant, or <c>null</c> when none could be resolved (tenant queries then return nothing).</summary>
    Guid? FoodBankId { get; }
}
