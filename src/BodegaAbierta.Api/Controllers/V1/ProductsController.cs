using Asp.Versioning;
using BodegaAbierta.Application.Common;
using BodegaAbierta.Application.Products;
using Microsoft.AspNetCore.Mvc;

namespace BodegaAbierta.Api.Controllers.V1;

[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/products")]
public sealed class ProductsController(ProductService products) : ApiControllerBase
{
    [HttpPost]
    [ProducesResponseType<ProductResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(CreateProductRequest request, CancellationToken cancellationToken)
    {
        var product = await products.CreateAsync(request, cancellationToken);
        SetETag(product.Version);
        return CreatedAtCollection(product.Id, product);
    }

    [HttpGet]
    public Task<PagedResult<ProductResponse>> List([FromQuery] ListProductsQuery query, CancellationToken cancellationToken)
        => products.ListAsync(query, cancellationToken);

    [HttpGet("{id:guid}")]
    [ProducesResponseType<ProductResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ProductResponse> Get(Guid id, CancellationToken cancellationToken)
    {
        var product = await products.GetAsync(id, cancellationToken);
        SetETag(product.Version);
        return product;
    }

    /// <summary>Requires If-Match with the ETag of the last GET (optimistic concurrency).</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType<ProductResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status412PreconditionFailed)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status428PreconditionRequired)]
    public async Task<ProductResponse> Update(Guid id, UpdateProductRequest request, CancellationToken cancellationToken)
    {
        var product = await products.UpdateAsync(id, request, ReadIfMatch(), cancellationToken);
        SetETag(product.Version);
        return product;
    }
}
