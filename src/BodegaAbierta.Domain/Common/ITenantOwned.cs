namespace BodegaAbierta.Domain.Common;

/// <summary>
/// Marks an entity that belongs to a single <see cref="FoodBanks.FoodBank"/> (tenant).
/// The persistence layer applies a global query filter to every type implementing this interface.
/// </summary>
public interface ITenantOwned
{
    Guid FoodBankId { get; }
}
