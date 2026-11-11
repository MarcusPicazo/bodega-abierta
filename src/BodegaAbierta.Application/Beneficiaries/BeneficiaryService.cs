using BodegaAbierta.Application.Abstractions;
using BodegaAbierta.Application.Common;
using BodegaAbierta.Domain.Beneficiaries;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace BodegaAbierta.Application.Beneficiaries;

public sealed record CreateBeneficiaryRequest(string Name, BeneficiaryType Type, string? Phone);

public sealed record ListBeneficiariesQuery(int Page = 1, int PageSize = Paging.DefaultPageSize);

public sealed record BeneficiaryResponse(Guid Id, string Name, BeneficiaryType Type, string? Phone, bool IsActive, string Version);

internal sealed class CreateBeneficiaryRequestValidator : AbstractValidator<CreateBeneficiaryRequest>
{
    public CreateBeneficiaryRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(Beneficiary.NameMaxLength);
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.Phone).MaximumLength(Beneficiary.PhoneMaxLength);
    }
}

public sealed class BeneficiaryService(
    IBodegaDbContext db,
    ITenantContext tenant,
    IValidator<CreateBeneficiaryRequest> validator)
{
    public async Task<BeneficiaryResponse> CreateAsync(CreateBeneficiaryRequest request, CancellationToken cancellationToken)
    {
        var foodBankId = tenant.RequireFoodBankId();
        await validator.ValidateAndThrowAsync(request, cancellationToken);

        var beneficiary = new Beneficiary(foodBankId, request.Name, request.Type, request.Phone);
        db.Beneficiaries.Add(beneficiary);
        await db.SaveChangesAsync(cancellationToken);
        return ToResponse(beneficiary);
    }

    public async Task<BeneficiaryResponse> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        tenant.RequireFoodBankId();
        var beneficiary = await db.Beneficiaries.AsNoTracking().FirstOrDefaultAsync(b => b.Id == id, cancellationToken)
            ?? throw new NotFoundException("Beneficiary", id);
        return ToResponse(beneficiary);
    }

    public Task<PagedResult<BeneficiaryResponse>> ListAsync(ListBeneficiariesQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        tenant.RequireFoodBankId();
        return db.Beneficiaries.AsNoTracking()
            .OrderBy(b => b.Name).ThenBy(b => b.Id)
            .ToPagedResultAsync(query.Page, query.PageSize, ToResponse, cancellationToken);
    }

    private static BeneficiaryResponse ToResponse(Beneficiary b)
        => new(b.Id, b.Name, b.Type, b.Phone, b.IsActive, VersionToken.Encode(b.RowVersion));
}
