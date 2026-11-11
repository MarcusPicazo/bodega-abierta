using BodegaAbierta.Application.Inventory;
using BodegaAbierta.Domain.Inventory;

namespace BodegaAbierta.Application.Intakes;

/// <param name="ReceivedAt">When the delivery arrived, with the local offset (e.g. -06:00). Defaults to now.</param>
public sealed record CreateDonationIntakeRequest(
    Guid DonorId,
    string ReceivedBy,
    DateTimeOffset? ReceivedAt,
    string? Notes,
    IReadOnlyList<DonationIntakeLineRequest> Lines);

public sealed record DonationIntakeLineRequest(
    Guid ProductId,
    Guid StorageLocationId,
    decimal Quantity,
    DateOnly? ExpiresOn);

public sealed record DonationIntakeResponse(
    Guid Id,
    Guid DonorId,
    string ReceivedBy,
    DateTimeOffset ReceivedAt,
    string? Notes,
    IReadOnlyList<InventoryLotResponse> Lots);

internal static class DonationIntakeMappings
{
    public static DonationIntakeResponse ToResponse(this DonationIntake intake)
        => new(
            intake.Id,
            intake.DonorId,
            intake.ReceivedBy,
            intake.ReceivedAt,
            intake.Notes,
            intake.Lots.Select(l => l.ToResponse()).ToList());
}
