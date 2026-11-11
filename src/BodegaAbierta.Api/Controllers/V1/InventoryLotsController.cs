using Asp.Versioning;
using BodegaAbierta.Application.Common;
using BodegaAbierta.Application.Inventory;
using Microsoft.AspNetCore.Mvc;

namespace BodegaAbierta.Api.Controllers.V1;

[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/inventory-lots")]
public sealed class InventoryLotsController(InventoryLotService lots) : ApiControllerBase
{
    /// <summary>Lots in FEFO order (earliest expiry first). Filters: productId, status, expiringOnOrBefore.</summary>
    [HttpGet]
    public Task<PagedResult<InventoryLotResponse>> List([FromQuery] ListInventoryLotsQuery query, CancellationToken cancellationToken)
        => lots.ListAsync(query, cancellationToken);

    /// <summary>Available lots expiring within the Active policy's ExpiryAlert days.</summary>
    [HttpGet("expiry-alerts")]
    public Task<ExpiryAlertsResponse> ExpiryAlerts(CancellationToken cancellationToken)
        => lots.GetExpiryAlertsAsync(cancellationToken);

    [HttpGet("{id:guid}")]
    [ProducesResponseType<InventoryLotResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<InventoryLotResponse> Get(Guid id, CancellationToken cancellationToken)
    {
        var lot = await lots.GetAsync(id, cancellationToken);
        SetETag(lot.Version);
        return lot;
    }
}
