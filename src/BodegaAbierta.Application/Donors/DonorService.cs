using BodegaAbierta.Application.Abstractions;
using BodegaAbierta.Application.Common;
using BodegaAbierta.Domain.Donors;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace BodegaAbierta.Application.Donors;

public sealed class DonorService(
    IBodegaDbContext db,
    ITenantContext tenant,
    IValidator<CreateDonorRequest> createValidator,
    IValidator<UpdateDonorRequest> updateValidator)
{
    public async Task<DonorResponse> CreateAsync(CreateDonorRequest request, CancellationToken cancellationToken)
    {
        var foodBankId = tenant.RequireFoodBankId();
        await createValidator.ValidateAndThrowAsync(request, cancellationToken);

        var donor = new Donor(foodBankId, request.Name, request.Type, request.Email, request.Phone);
        db.Donors.Add(donor);
        await db.SaveChangesAsync(cancellationToken);

        return donor.ToResponse();
    }

    public async Task<DonorResponse> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        tenant.RequireFoodBankId();
        var donor = await db.Donors.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id, cancellationToken)
            ?? throw new NotFoundException("Donor", id);

        return donor.ToResponse();
    }

    public Task<PagedResult<DonorResponse>> ListAsync(ListDonorsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        tenant.RequireFoodBankId();

        IQueryable<Donor> donors = db.Donors.AsNoTracking();

        // TODO(Marcus): when query.Search has text, keep only donors whose Name contains it
        // (EF translates string.Contains to LIKE '%...%'). Trim it first and ignore blank input.
        // Remove the Skip from DonorEndpointsTests.List_filters_by_search to check your work.

        return donors
            .OrderBy(d => d.Name).ThenBy(d => d.Id)
            .ToPagedResultAsync(query.Page, query.PageSize, d => d.ToResponse(), cancellationToken);
    }

    public async Task<DonorResponse> UpdateAsync(
        Guid id, UpdateDonorRequest request, byte[] expectedVersion, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        tenant.RequireFoodBankId();
        await updateValidator.ValidateAndThrowAsync(request, cancellationToken);

        var donor = await db.Donors.FirstOrDefaultAsync(d => d.Id == id, cancellationToken)
            ?? throw new NotFoundException("Donor", id);

        db.SetExpectedRowVersion(donor, expectedVersion);
        donor.Update(request.Name, request.Type, request.Email, request.Phone, request.IsActive);
        await db.SaveChangesAsync(cancellationToken); // DbUpdateConcurrencyException -> 412

        return donor.ToResponse(); // RowVersion was refreshed by SaveChanges
    }
}
