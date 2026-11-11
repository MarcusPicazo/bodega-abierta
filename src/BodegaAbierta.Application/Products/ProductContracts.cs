using BodegaAbierta.Application.Common;
using BodegaAbierta.Domain.Products;

namespace BodegaAbierta.Application.Products;

public sealed record CreateProductRequest(string Name, ProductCategory Category, UnitOfMeasure Unit, bool IsPerishable);

public sealed record UpdateProductRequest(string Name, ProductCategory Category, UnitOfMeasure Unit, bool IsPerishable);

public sealed record ListProductsQuery(ProductCategory? Category = null, int Page = 1, int PageSize = Paging.DefaultPageSize);

public sealed record ProductResponse(
    Guid Id,
    string Name,
    ProductCategory Category,
    UnitOfMeasure Unit,
    bool IsPerishable,
    string Version);

internal static class ProductMappings
{
    public static ProductResponse ToResponse(this Product product)
        => new(product.Id, product.Name, product.Category, product.Unit, product.IsPerishable, VersionToken.Encode(product.RowVersion));
}
