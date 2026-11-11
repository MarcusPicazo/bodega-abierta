using BodegaAbierta.Domain.Products;
using FluentValidation;

namespace BodegaAbierta.Application.Products;

internal sealed class CreateProductRequestValidator : AbstractValidator<CreateProductRequest>
{
    public CreateProductRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(Product.NameMaxLength);
        RuleFor(x => x.Category).IsInEnum();
        RuleFor(x => x.Unit).IsInEnum();
    }
}

internal sealed class UpdateProductRequestValidator : AbstractValidator<UpdateProductRequest>
{
    public UpdateProductRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(Product.NameMaxLength);
        RuleFor(x => x.Category).IsInEnum();
        RuleFor(x => x.Unit).IsInEnum();
    }
}
