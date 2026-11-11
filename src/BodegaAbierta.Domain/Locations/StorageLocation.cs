using BodegaAbierta.Domain.Common;

namespace BodegaAbierta.Domain.Locations;

/// <summary>A physical place inside a food bank's warehouse (shelf, cold room, freezer).</summary>
public sealed class StorageLocation : Entity, ITenantOwned
{
    public const int CodeMaxLength = 32;
    public const int NameMaxLength = 200;

    private StorageLocation() { } // EF Core

    public StorageLocation(Guid foodBankId, string code, string name, StorageType storageType)
    {
        if (foodBankId == Guid.Empty)
        {
            throw new ArgumentException("A storage location must belong to a food bank.", nameof(foodBankId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        FoodBankId = foodBankId;
        Code = code.Trim().ToUpperInvariant();
        Name = name.Trim();
        StorageType = storageType;
    }

    public Guid FoodBankId { get; private set; }

    public string Code { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public StorageType StorageType { get; private set; }
}
