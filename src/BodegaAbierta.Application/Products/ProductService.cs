using BodegaAbierta.Application.Abstractions;
using BodegaAbierta.Application.Common;
using BodegaAbierta.Domain.Products;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace BodegaAbierta.Application.Products;

public sealed class ProductService(
    IBodegaDbContext db,
    ITenantContext tenant,
    IValidator<CreateProductRequest> createValidator,
    IValidator<UpdateProductRequest> updateValidator)
{
    public async Task<ProductResponse> CreateAsync(CreateProductRequest request, CancellationToken cancellationToken)
    {
        var foodBankId = tenant.RequireFoodBankId();
        await createValidator.ValidateAndThrowAsync(request, cancellationToken);

        var product = new Product(foodBankId, request.Name, request.Category, request.Unit, request.IsPerishable);
        db.Products.Add(product);
        await db.SaveChangesAsync(cancellationToken); // duplicate name -> unique index -> 409

        return product.ToResponse();
    }

    public async Task<ProductResponse> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        tenant.RequireFoodBankId();
        var product = await db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new NotFoundException("Product", id);

        return product.ToResponse();
    }

    public Task<PagedResult<ProductResponse>> ListAsync(ListProductsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        tenant.RequireFoodBankId();

        var products = db.Products.AsNoTracking();
        if (query.Category is { } category)
        {
            products = products.Where(p => p.Category == category);
        }

        return products
            .OrderBy(p => p.Name).ThenBy(p => p.Id)
            .ToPagedResultAsync(query.Page, query.PageSize, p => p.ToResponse(), cancellationToken);
    }

    public async Task<ProductResponse> UpdateAsync(
        Guid id, UpdateProductRequest request, byte[] expectedVersion, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        tenant.RequireFoodBankId();
        await updateValidator.ValidateAndThrowAsync(request, cancellationToken);

        var product = await db.Products.FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new NotFoundException("Product", id);

        db.SetExpectedRowVersion(product, expectedVersion);
        product.Update(request.Name, request.Category, request.Unit, request.IsPerishable);
        await db.SaveChangesAsync(cancellationToken);

        return product.ToResponse();
    }
}
