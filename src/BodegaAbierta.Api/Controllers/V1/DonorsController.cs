using Asp.Versioning;
using BodegaAbierta.Application.Common;
using BodegaAbierta.Application.Donors;
using Microsoft.AspNetCore.Mvc;

namespace BodegaAbierta.Api.Controllers.V1;

[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/donors")]
public sealed class DonorsController(DonorService donors) : ApiControllerBase
{
    [HttpPost]
    [ProducesResponseType<DonorResponse>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(CreateDonorRequest request, CancellationToken cancellationToken)
    {
        var donor = await donors.CreateAsync(request, cancellationToken);
        SetETag(donor.Version);
        return CreatedAtCollection(donor.Id, donor);
    }

    [HttpGet]
    public Task<PagedResult<DonorResponse>> List([FromQuery] ListDonorsQuery query, CancellationToken cancellationToken)
        => donors.ListAsync(query, cancellationToken);

    [HttpGet("{id:guid}")]
    [ProducesResponseType<DonorResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<DonorResponse> Get(Guid id, CancellationToken cancellationToken)
    {
        var donor = await donors.GetAsync(id, cancellationToken);
        SetETag(donor.Version);
        return donor;
    }

    /// <summary>Requires If-Match with the ETag of the last GET (optimistic concurrency).</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType<DonorResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status412PreconditionFailed)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status428PreconditionRequired)]
    public async Task<DonorResponse> Update(Guid id, UpdateDonorRequest request, CancellationToken cancellationToken)
    {
        var donor = await donors.UpdateAsync(id, request, ReadIfMatch(), cancellationToken);
        SetETag(donor.Version);
        return donor;
    }
}
