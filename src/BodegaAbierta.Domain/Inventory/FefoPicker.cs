namespace BodegaAbierta.Domain.Inventory;

/// <summary>One lot and the quantity taken from it.</summary>
public sealed record LotPick(InventoryLot Lot, decimal Quantity);

/// <summary>Result of planning one product line: what was picked and what could not be covered.</summary>
public sealed record FefoPlan(IReadOnlyList<LotPick> Picks, decimal Shortfall)
{
    public bool IsComplete => Shortfall == 0;
}

/// <summary>
/// First Expired, First Out. A pure function (no database), so it is trivially unit-testable:
/// order by expiry (no expiry = last), then by receipt time, then by id for a stable order; skip lots
/// that are unusable on the pickup date; take greedily until the requested quantity is covered.
/// </summary>
public static class FefoPicker
{
    public static IOrderedEnumerable<InventoryLot> Order(IEnumerable<InventoryLot> lots)
        => lots
            .OrderBy(l => l.ExpiresOn ?? DateOnly.MaxValue)
            .ThenBy(l => l.ReceivedAt)
            .ThenBy(l => l.Id);

    public static FefoPlan Pick(IEnumerable<InventoryLot> candidates, decimal quantity, DateOnly pickupDate)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);

        var picks = new List<LotPick>();
        var remaining = quantity;

        foreach (var lot in Order(candidates.Where(l => l.IsAllocatableOn(pickupDate))))
        {
            if (remaining == 0)
            {
                break;
            }

            var take = Math.Min(lot.QuantityOnHand, remaining);
            picks.Add(new LotPick(lot, take));
            remaining -= take;
        }

        return new FefoPlan(picks, remaining);
    }
}
