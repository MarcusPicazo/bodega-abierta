using BodegaAbierta.Domain.Donors;
using FluentValidation;

namespace BodegaAbierta.Application.Donors;

internal sealed class CreateDonorRequestValidator : AbstractValidator<CreateDonorRequest>
{
    public CreateDonorRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(Donor.NameMaxLength);
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.Email).EmailAddress().MaximumLength(Donor.EmailMaxLength).When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.Phone).MaximumLength(Donor.PhoneMaxLength);
    }
}

internal sealed class UpdateDonorRequestValidator : AbstractValidator<UpdateDonorRequest>
{
    public UpdateDonorRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(Donor.NameMaxLength);
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.Email).EmailAddress().MaximumLength(Donor.EmailMaxLength).When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.Phone).MaximumLength(Donor.PhoneMaxLength);
    }
}
