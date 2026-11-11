using BodegaAbierta.Application.Common;
using BodegaAbierta.Domain.Inventory;
using BodegaAbierta.Domain.Products;

namespace BodegaAbierta.Application.Inventory;

public sealed record ListInventoryLotsQuery(
    Guid? ProductId = null,
    LotStatus? Status = null,
    DateOnly? ExpiringOnOrBefore = null,
    int Page = 1,
    int PageSize = Paging.DefaultPageSize);

public sealed record InventoryLotResponse(
    Guid Id,
    Guid ProductId,
    Guid DonationIntakeId,
    Guid StorageLocationId,
    decimal InitialQuantity,
    decimal QuantityOnHand,
    UnitOfMeasure Unit,
    DateOnly? ExpiresOn,
    LotStatus Status,
    DateTimeOffset ReceivedAt,
    string Version);

internal static class InventoryLotMappings
{
    public static InventoryLotResponse ToResponse(this InventoryLot lot)
        => new(
            lot.Id,
            lot.ProductId,
            lot.DonationIntakeId,
            lot.StorageLocationId,
            lot.InitialQuantity,
            lot.QuantityOnHand,
            lot.Unit,
            lot.ExpiresOn,
            lot.Status,
            lot.ReceivedAt,
            VersionToken.Encode(lot.RowVersion));
}
