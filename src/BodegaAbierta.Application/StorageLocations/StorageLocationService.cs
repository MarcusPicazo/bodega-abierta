using BodegaAbierta.Application.Abstractions;
using BodegaAbierta.Domain.Locations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace BodegaAbierta.Application.StorageLocations;

public sealed record CreateStorageLocationRequest(string Code, string Name, StorageType StorageType);

public sealed record StorageLocationResponse(Guid Id, string Code, string Name, StorageType StorageType);

internal sealed class CreateStorageLocationRequestValidator : AbstractValidator<CreateStorageLocationRequest>
{
    public CreateStorageLocationRequestValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(StorageLocation.CodeMaxLength);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(StorageLocation.NameMaxLength);
        RuleFor(x => x.StorageType).IsInEnum();
    }
}

public sealed class StorageLocationService(
    IBodegaDbContext db,
    ITenantContext tenant,
    IValidator<CreateStorageLocationRequest> createValidator)
{
    public async Task<StorageLocationResponse> CreateAsync(CreateStorageLocationRequest request, CancellationToken cancellationToken)
    {
        var foodBankId = tenant.RequireFoodBankId();
        await createValidator.ValidateAndThrowAsync(request, cancellationToken);

        var location = new StorageLocation(foodBankId, request.Code, request.Name, request.StorageType);
        db.StorageLocations.Add(location);
        await db.SaveChangesAsync(cancellationToken); // duplicate code -> unique index -> 409

        return ToResponse(location);
    }

    public async Task<IReadOnlyList<StorageLocationResponse>> ListAsync(CancellationToken cancellationToken)
    {
        tenant.RequireFoodBankId();
        var locations = await db.StorageLocations.AsNoTracking().OrderBy(l => l.Code).ToListAsync(cancellationToken);
        return locations.Select(ToResponse).ToList();
    }

    private static StorageLocationResponse ToResponse(StorageLocation l) => new(l.Id, l.Code, l.Name, l.StorageType);
}
