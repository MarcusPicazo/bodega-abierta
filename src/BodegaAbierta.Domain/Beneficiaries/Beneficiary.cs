using BodegaAbierta.Domain.Common;

namespace BodegaAbierta.Domain.Beneficiaries;

public enum BeneficiaryType
{
    Household = 0,
    Organization = 1,
}

/// <summary>Who receives food: a household or an organization (shelter, community kitchen...).</summary>
public sealed class Beneficiary : VersionedEntity, ITenantOwned
{
    public const int NameMaxLength = 200;
    public const int PhoneMaxLength = 32;

    private Beneficiary() { } // EF Core

    public Beneficiary(Guid foodBankId, string name, BeneficiaryType type, string? phone)
    {
        if (foodBankId == Guid.Empty)
        {
            throw new ArgumentException("A beneficiary must belong to a food bank.", nameof(foodBankId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        FoodBankId = foodBankId;
        Name = name.Trim();
        Type = type;
        Phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();
        IsActive = true;
    }

    public Guid FoodBankId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public BeneficiaryType Type { get; private set; }

    public string? Phone { get; private set; }

    public bool IsActive { get; private set; }

    public void SetActive(bool isActive) => IsActive = isActive;
}
