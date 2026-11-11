using Asp.Versioning;
using BodegaAbierta.Application.Intakes;
using Microsoft.AspNetCore.Mvc;

namespace BodegaAbierta.Api.Controllers.V1;

[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/donation-intakes")]
public sealed class DonationIntakesController(DonationIntakeService intakes) : ApiControllerBase
{
    /// <summary>Registers a delivery from a donor; creates one inventory lot per line (all or nothing).</summary>
    [HttpPost]
    [ProducesResponseType<DonationIntakeResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create(CreateDonationIntakeRequest request, CancellationToken cancellationToken)
    {
        var intake = await intakes.CreateAsync(request, cancellationToken);
        return CreatedAtCollection(intake.Id, intake);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<DonationIntakeResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<DonationIntakeResponse> Get(Guid id, CancellationToken cancellationToken)
        => intakes.GetAsync(id, cancellationToken);
}
