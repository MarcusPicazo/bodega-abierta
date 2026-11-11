using BodegaAbierta.Domain.Common;

namespace BodegaAbierta.Domain.FoodBanks;

/// <summary>The tenant. Every tenant-owned row points to one food bank.</summary>
public sealed class FoodBank : Entity
{
    public const int NameMaxLength = 200;

    private FoodBank() { } // EF Core

    public FoodBank(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name.Trim();
    }

    public string Name { get; private set; } = string.Empty;
}
