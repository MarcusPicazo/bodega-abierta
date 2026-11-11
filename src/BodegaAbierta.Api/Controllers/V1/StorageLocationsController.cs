using Asp.Versioning;
using BodegaAbierta.Application.StorageLocations;
using Microsoft.AspNetCore.Mvc;

namespace BodegaAbierta.Api.Controllers.V1;

[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/storage-locations")]
public sealed class StorageLocationsController(StorageLocationService locations) : ApiControllerBase
{
    [HttpPost]
    [ProducesResponseType<StorageLocationResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(CreateStorageLocationRequest request, CancellationToken cancellationToken)
    {
        var location = await locations.CreateAsync(request, cancellationToken);
        return CreatedAtCollection(location.Id, location);
    }

    [HttpGet]
    public Task<IReadOnlyList<StorageLocationResponse>> List(CancellationToken cancellationToken)
        => locations.ListAsync(cancellationToken);
}
