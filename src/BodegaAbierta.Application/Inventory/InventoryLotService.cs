using BodegaAbierta.Application.Abstractions;
using BodegaAbierta.Application.Common;
using BodegaAbierta.Application.Rules;
using BodegaAbierta.Application.Rules.Evaluators;
using BodegaAbierta.Domain.Inventory;
using BodegaAbierta.Domain.Policies;
using Microsoft.EntityFrameworkCore;

namespace BodegaAbierta.Application.Inventory;

public sealed record ExpiryAlertsResponse(int? DaysBeforeExpiry, DateOnly AsOf, IReadOnlyList<InventoryLotResponse> Lots);

public sealed class InventoryLotService(IBodegaDbContext db, ITenantContext tenant, PolicyGate policyGate, TimeProvider timeProvider)
{
    /// <summary>
    /// Available lots expiring within the Active policy's ExpiryAlert days (FEFO order). Without an
    /// ExpiryAlert rule there is nothing to flag: DaysBeforeExpiry is null and the list is empty.
    /// </summary>
    public async Task<ExpiryAlertsResponse> GetExpiryAlertsAsync(CancellationToken cancellationToken)
    {
        tenant.RequireFoodBankId();
        var today = WeeklyWindow.LocalToday(timeProvider.GetUtcNow());

        var policy = await policyGate.GetActivePolicyAsync(cancellationToken);
        var rule = policy?.Rules.FirstOrDefault(r => r.Type == RuleType.ExpiryAlert);
        if (rule is null)
        {
            return new ExpiryAlertsResponse(null, today, []);
        }

        var days = RuleParameters.Parse<ExpiryAlertParameters>(rule.ParametersJson).DaysBeforeExpiry;
        var limit = today.AddDays(days);

        var lots = await db.InventoryLots.AsNoTracking()
            .Where(l => l.Status == LotStatus.Available && l.QuantityOnHand > 0 && l.ExpiresOn != null && l.ExpiresOn <= limit)
            .OrderBy(l => l.ExpiresOn).ThenBy(l => l.ReceivedAt).ThenBy(l => l.Id)
            .Take(500)
            .ToListAsync(cancellationToken);

        return new ExpiryAlertsResponse(days, today, lots.Select(l => l.ToResponse()).ToList());
    }

    public async Task<InventoryLotResponse> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        tenant.RequireFoodBankId();
        var lot = await db.InventoryLots.AsNoTracking().FirstOrDefaultAsync(l => l.Id == id, cancellationToken)
            ?? throw new NotFoundException("Inventory lot", id);

        return lot.ToResponse();
    }

    /// <summary>Lists lots in FEFO order: earliest expiry first, lots without expiry last.</summary>
    public Task<PagedResult<InventoryLotResponse>> ListAsync(ListInventoryLotsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        tenant.RequireFoodBankId();

        IQueryable<InventoryLot> lots = db.InventoryLots.AsNoTracking();

        if (query.ProductId is { } productId)
        {
            lots = lots.Where(l => l.ProductId == productId);
        }

        if (query.Status is { } status)
        {
            lots = lots.Where(l => l.Status == status);
        }

        if (query.ExpiringOnOrBefore is { } date)
        {
            lots = lots.Where(l => l.ExpiresOn != null && l.ExpiresOn <= date);
        }

        return lots
            .OrderBy(l => l.ExpiresOn == null)
            .ThenBy(l => l.ExpiresOn)
            .ThenBy(l => l.ReceivedAt)
            .ThenBy(l => l.Id)
            .ToPagedResultAsync(query.Page, query.PageSize, l => l.ToResponse(), cancellationToken);
    }
}
