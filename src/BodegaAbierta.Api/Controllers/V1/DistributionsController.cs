using Asp.Versioning;
using BodegaAbierta.Application.Beneficiaries;
using BodegaAbierta.Application.Common;
using BodegaAbierta.Application.Distributions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BodegaAbierta.Api.Controllers.V1;

[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/distributions")]
[Authorize]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
public sealed class DistributionsController(DistributionService distributions) : ApiControllerBase
{
    /// <summary>
    /// Allocates lots by FEFO and books the pickup slot, if the Active policy allows it.
    /// 422 with "violations" when it does not; 409 when stock or slot changed concurrently (retry).
    /// </summary>
    [HttpPost]
    [ProducesResponseType<DistributionResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create(CreateDistributionRequest request, CancellationToken cancellationToken)
    {
        var distribution = await distributions.CreateAsync(request, cancellationToken);
        return CreatedAtCollection(distribution.Id, distribution);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<DistributionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<DistributionResponse> Get(Guid id, CancellationToken cancellationToken)
        => distributions.GetAsync(id, cancellationToken);
}

[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/pickup-slots")]
[Authorize]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
public sealed class PickupSlotsController(PickupSlotService slots) : ApiControllerBase
{
    /// <summary>Creates a slot; 422 if the Active policy's PickupWindow does not allow it.</summary>
    [HttpPost]
    [ProducesResponseType<PickupSlotResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create(CreatePickupSlotRequest request, CancellationToken cancellationToken)
    {
        var slot = await slots.CreateAsync(request, cancellationToken);
        SetETag(slot.Version);
        return CreatedAtCollection(slot.Id, slot);
    }

    [HttpGet]
    public Task<IReadOnlyList<PickupSlotResponse>> List([FromQuery] ListPickupSlotsQuery query, CancellationToken cancellationToken)
        => slots.ListAsync(query, cancellationToken);

    [HttpGet("{id:guid}")]
    [ProducesResponseType<PickupSlotResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<PickupSlotResponse> Get(Guid id, CancellationToken cancellationToken)
    {
        var slot = await slots.GetAsync(id, cancellationToken);
        SetETag(slot.Version);
        return slot;
    }
}

[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/beneficiaries")]
public sealed class BeneficiariesController(BeneficiaryService beneficiaries) : ApiControllerBase
{
    [HttpPost]
    [ProducesResponseType<BeneficiaryResponse>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(CreateBeneficiaryRequest request, CancellationToken cancellationToken)
    {
        var beneficiary = await beneficiaries.CreateAsync(request, cancellationToken);
        SetETag(beneficiary.Version);
        return CreatedAtCollection(beneficiary.Id, beneficiary);
    }

    [HttpGet]
    public Task<PagedResult<BeneficiaryResponse>> List([FromQuery] ListBeneficiariesQuery query, CancellationToken cancellationToken)
        => beneficiaries.ListAsync(query, cancellationToken);

    [HttpGet("{id:guid}")]
    [ProducesResponseType<BeneficiaryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<BeneficiaryResponse> Get(Guid id, CancellationToken cancellationToken)
    {
        var beneficiary = await beneficiaries.GetAsync(id, cancellationToken);
        SetETag(beneficiary.Version);
        return beneficiary;
    }
}
