using BodegaAbierta.Domain.Common;

namespace BodegaAbierta.Domain.Distributions;

/// <summary>
/// A time window in which beneficiaries pick up their distributions. <see cref="ReservedCount"/> is
/// stored (not computed) so that booking the last spot updates this row: its rowversion then turns two
/// simultaneous bookings into one success and one concurrency conflict instead of an overbooked slot.
/// </summary>
public sealed class PickupSlot : VersionedEntity, ITenantOwned
{
    public const int MaxCapacity = 500;

    private PickupSlot() { } // EF Core

    public PickupSlot(Guid foodBankId, DateTimeOffset startsAt, DateTimeOffset endsAt, int capacity)
    {
        if (foodBankId == Guid.Empty)
        {
            throw new ArgumentException("A pickup slot must belong to a food bank.", nameof(foodBankId));
        }

        if (endsAt <= startsAt)
        {
            throw new DomainRuleException("slot.invalid_window", "A pickup slot must end after it starts.");
        }

        if (capacity is < 1 or > MaxCapacity)
        {
            throw new DomainRuleException("slot.invalid_capacity", $"Capacity must be between 1 and {MaxCapacity}.");
        }

        FoodBankId = foodBankId;
        StartsAt = startsAt;
        EndsAt = endsAt;
        Capacity = capacity;
    }

    public Guid FoodBankId { get; private set; }

    public DateTimeOffset StartsAt { get; private set; }

    public DateTimeOffset EndsAt { get; private set; }

    public int Capacity { get; private set; }

    public int ReservedCount { get; private set; }

    public bool IsFull => ReservedCount >= Capacity;

    /// <summary>Calendar date of the pickup, in the slot's own offset (used for expiry checks).</summary>
    public DateOnly PickupDate => DateOnly.FromDateTime(StartsAt.DateTime);

    internal void Reserve()
    {
        if (IsFull)
        {
            throw new DomainRuleException("slot.full", $"The pickup slot starting {StartsAt:yyyy-MM-dd HH:mm} is full.");
        }

        ReservedCount++;
    }

    // TODO(Marcus) [Phase 4]: add `internal void Release()` for Distribution.Cancel: ReservedCount--, but
    // never below 0 (InvalidOperationException: that would be a bug, not a business rule).
}
