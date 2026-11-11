using BodegaAbierta.Application.Common;
using BodegaAbierta.Domain.Donors;

namespace BodegaAbierta.Application.Donors;

public sealed record CreateDonorRequest(string Name, DonorType Type, string? Email, string? Phone);

public sealed record UpdateDonorRequest(string Name, DonorType Type, string? Email, string? Phone, bool IsActive);

public sealed record ListDonorsQuery(string? Search = null, int Page = 1, int PageSize = Paging.DefaultPageSize);

public sealed record DonorResponse(
    Guid Id,
    string Name,
    DonorType Type,
    string? Email,
    string? Phone,
    bool IsActive,
    string Version);

internal static class DonorMappings
{
    public static DonorResponse ToResponse(this Donor donor)
        => new(donor.Id, donor.Name, donor.Type, donor.Email, donor.Phone, donor.IsActive, VersionToken.Encode(donor.RowVersion));
}
