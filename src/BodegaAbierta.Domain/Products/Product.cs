using BodegaAbierta.Domain.Common;

namespace BodegaAbierta.Domain.Products;

public enum ProductCategory
{
    Other = 0,
    Grains = 1,
    FruitsAndVegetables = 2,
    Dairy = 3,
    MeatAndFish = 4,
    Canned = 5,
    Bakery = 6,
    Beverages = 7,
    Hygiene = 8,
}

public enum UnitOfMeasure
{
    Kilogram = 0,
    Unit = 1,
}

/// <summary>A catalog item a food bank receives and distributes (e.g. "Frijol negro", "Leche entera 1 L").</summary>
public sealed class Product : VersionedEntity, ITenantOwned
{
    public const int NameMaxLength = 200;

    private Product() { } // EF Core

    public Product(Guid foodBankId, string name, ProductCategory category, UnitOfMeasure unit, bool isPerishable)
    {
        if (foodBankId == Guid.Empty)
        {
            throw new ArgumentException("A product must belong to a food bank.", nameof(foodBankId));
        }

        FoodBankId = foodBankId;
        Update(name, category, unit, isPerishable);
    }

    public Guid FoodBankId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public ProductCategory Category { get; private set; }

    public UnitOfMeasure Unit { get; private set; }

    /// <summary>Perishable products require an expiry date on every lot received.</summary>
    public bool IsPerishable { get; private set; }

    public void Update(string name, ProductCategory category, UnitOfMeasure unit, bool isPerishable)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name.Trim();
        Category = category;
        Unit = unit;
        IsPerishable = isPerishable;
    }
}
