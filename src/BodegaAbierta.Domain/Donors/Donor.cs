using BodegaAbierta.Domain.Common;

namespace BodegaAbierta.Domain.Donors;

public enum DonorType
{
    Individual = 0,
    Business = 1,
    Government = 2,
    NonProfit = 3,
}

/// <summary>A person or organization that donates food to a food bank.</summary>
public sealed class Donor : VersionedEntity, ITenantOwned
{
    public const int NameMaxLength = 200;
    public const int EmailMaxLength = 256;
    public const int PhoneMaxLength = 32;

    private Donor() { } // EF Core

    public Donor(Guid foodBankId, string name, DonorType type, string? email, string? phone)
    {
        if (foodBankId == Guid.Empty)
        {
            throw new ArgumentException("A donor must belong to a food bank.", nameof(foodBankId));
        }

        FoodBankId = foodBankId;
        IsActive = true;
        SetDetails(name, type, email, phone);
    }

    public Guid FoodBankId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public DonorType Type { get; private set; }

    public string? Email { get; private set; }

    public string? Phone { get; private set; }

    public bool IsActive { get; private set; }

    public void Update(string name, DonorType type, string? email, string? phone, bool isActive)
    {
        SetDetails(name, type, email, phone);
        IsActive = isActive;
    }

    /// <summary>Guards intake: an inactive donor cannot deliver new donations.</summary>
    public void EnsureCanDonate()
    {
        if (!IsActive)
        {
            throw new DomainRuleException("donor.inactive", $"Donor '{Name}' is inactive and cannot deliver donations.");
        }
    }

    private void SetDetails(string name, DonorType type, string? email, string? phone)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name.Trim();
        Type = type;
        Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim();
        Phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();
    }
}
