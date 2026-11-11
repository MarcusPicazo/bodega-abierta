using BodegaAbierta.Application.Abstractions;
using BodegaAbierta.Application.Common;
using BodegaAbierta.Domain.Inventory;
using Microsoft.EntityFrameworkCore;

namespace BodegaAbierta.Application.Inventory;

public sealed class InventoryLotService(IBodegaDbContext db, ITenantContext tenant)
{
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
