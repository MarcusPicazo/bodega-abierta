using BodegaAbierta.Application.Abstractions;
using BodegaAbierta.Application.Common;
using BodegaAbierta.Domain.Inventory;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;

namespace BodegaAbierta.Application.Intakes;

public sealed class DonationIntakeService(
    IBodegaDbContext db,
    ITenantContext tenant,
    IValidator<CreateDonationIntakeRequest> validator,
    TimeProvider timeProvider)
{
    /// <summary>
    /// Registers a delivery and creates one inventory lot per line. The intake and all its lots are
    /// inserted by a single SaveChanges call, i.e. one database transaction: all or nothing.
    /// </summary>
    public async Task<DonationIntakeResponse> CreateAsync(CreateDonationIntakeRequest request, CancellationToken cancellationToken)
    {
        tenant.RequireFoodBankId();
        await validator.ValidateAndThrowAsync(request, cancellationToken);

        // The query filter already limits these lookups to the current food bank, so an id that
        // belongs to another tenant simply "does not exist" here.
        var donor = await db.Donors.FirstOrDefaultAsync(d => d.Id == request.DonorId, cancellationToken);

        var productIds = request.Lines.Select(l => l.ProductId).Distinct().ToList();
        var products = await db.Products
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        var locationIds = request.Lines.Select(l => l.StorageLocationId).Distinct().ToList();
        var knownLocations = (await db.StorageLocations
            .Where(l => locationIds.Contains(l.Id))
            .Select(l => l.Id)
            .ToListAsync(cancellationToken)).ToHashSet();

        var failures = new List<ValidationFailure>();
        if (donor is null)
        {
            failures.Add(new ValidationFailure("donorId", $"Donor '{request.DonorId}' was not found."));
        }

        for (var i = 0; i < request.Lines.Count; i++)
        {
            var line = request.Lines[i];
            if (!products.ContainsKey(line.ProductId))
            {
                failures.Add(new ValidationFailure($"lines[{i}].productId", $"Product '{line.ProductId}' was not found."));
            }

            if (!knownLocations.Contains(line.StorageLocationId))
            {
                failures.Add(new ValidationFailure(
                    $"lines[{i}].storageLocationId", $"Storage location '{line.StorageLocationId}' was not found."));
            }
        }

        if (failures.Count > 0)
        {
            throw new ValidationException(failures);
        }

        var intake = new DonationIntake(donor!, request.ReceivedBy, request.ReceivedAt ?? timeProvider.GetUtcNow(), request.Notes);
        foreach (var line in request.Lines)
        {
            intake.AddLot(products[line.ProductId], line.StorageLocationId, line.Quantity, line.ExpiresOn);
        }

        db.DonationIntakes.Add(intake);
        await db.SaveChangesAsync(cancellationToken);

        return intake.ToResponse();
    }

    public async Task<DonationIntakeResponse> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        tenant.RequireFoodBankId();
        var intake = await db.DonationIntakes
            .AsNoTracking()
            .Include(i => i.Lots)
            .FirstOrDefaultAsync(i => i.Id == id, cancellationToken)
            ?? throw new NotFoundException("Donation intake", id);

        return intake.ToResponse();
    }
}
