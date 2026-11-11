using BodegaAbierta.Domain.Inventory;
using FluentValidation;

namespace BodegaAbierta.Application.Intakes;

/// <summary>
/// Shape of the request only. Rules that need the database or entity state (does the product exist,
/// is it perishable, is the donor active) are enforced by the service and the domain.
/// </summary>
internal sealed class CreateDonationIntakeRequestValidator : AbstractValidator<CreateDonationIntakeRequest>
{
    public const int MaxLines = 100;

    /// <summary>Tolerance for clocks of tablets/phones in the warehouse that run slightly ahead.</summary>
    private static readonly TimeSpan ClockSkew = TimeSpan.FromMinutes(5);

    public CreateDonationIntakeRequestValidator(TimeProvider timeProvider)
    {
        RuleFor(x => x.DonorId).NotEmpty();
        RuleFor(x => x.ReceivedBy).NotEmpty().MaximumLength(DonationIntake.ReceivedByMaxLength);
        RuleFor(x => x.Notes).MaximumLength(DonationIntake.NotesMaxLength);

        RuleFor(x => x.ReceivedAt)
            .Must(receivedAt => receivedAt is null || receivedAt <= timeProvider.GetUtcNow() + ClockSkew)
            .WithMessage("'Received At' cannot be in the future.");

        RuleFor(x => x.Lines)
            .NotEmpty().WithMessage("An intake needs at least one line.")
            .Must(lines => lines is null || lines.Count <= MaxLines).WithMessage($"An intake can have at most {MaxLines} lines.");

        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.ProductId).NotEmpty();
            line.RuleFor(l => l.StorageLocationId).NotEmpty();
            line.RuleFor(l => l.Quantity).GreaterThan(0).PrecisionScale(18, 3, ignoreTrailingZeros: true);
        });
    }
}
