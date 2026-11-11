using Asp.Versioning;
using BodegaAbierta.Api.Auth;
using BodegaAbierta.Application.Policies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BodegaAbierta.Api.Controllers.V1;

/// <summary>Versioned policy sets: Draft -&gt; PendingApproval -&gt; Active -&gt; Retired.</summary>
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/policies")]
[Authorize]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
public sealed class PoliciesController(PolicySetService policies) : ApiControllerBase
{
    /// <summary>Creates a new Draft version (optionally copied from another version).</summary>
    [HttpPost]
    [ProducesResponseType<PolicySetResponse>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(CreatePolicySetRequest request, CancellationToken cancellationToken)
    {
        var policy = await policies.CreateDraftAsync(request, cancellationToken);
        SetETag(policy.RowVersion);
        return CreatedAtCollection(policy.Id, policy);
    }

    [HttpGet]
    public Task<IReadOnlyList<PolicySetResponse>> List(CancellationToken cancellationToken)
        => policies.ListAsync(cancellationToken);

    [HttpGet("active")]
    [ProducesResponseType<PolicySetResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<PolicySetResponse> GetActive(CancellationToken cancellationToken)
        => WithETag(await policies.GetActiveAsync(cancellationToken));

    [HttpGet("{id:guid}")]
    [ProducesResponseType<PolicySetResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<PolicySetResponse> Get(Guid id, CancellationToken cancellationToken)
        => WithETag(await policies.GetAsync(id, cancellationToken));

    /// <summary>Edits a Draft. Requires If-Match.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType<PolicySetResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status412PreconditionFailed)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<PolicySetResponse> Update(Guid id, UpdatePolicySetRequest request, CancellationToken cancellationToken)
        => WithETag(await policies.UpdateDraftAsync(id, request, ReadIfMatch(), cancellationToken));

    /// <summary>Draft -&gt; PendingApproval. Requires If-Match.</summary>
    [HttpPost("{id:guid}/submit")]
    [ProducesResponseType<PolicySetResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<PolicySetResponse> Submit(Guid id, CancellationToken cancellationToken)
        => WithETag(await policies.SubmitAsync(id, ReadIfMatch(), cancellationToken));

    /// <summary>PendingApproval -&gt; Active (retires the previous Active version). Coordinators only. Requires If-Match.</summary>
    [HttpPost("{id:guid}/approve")]
    [Authorize(Policy = AuthPolicies.Coordinator)]
    [ProducesResponseType<PolicySetResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<PolicySetResponse> Approve(Guid id, CancellationToken cancellationToken)
        => WithETag(await policies.ApproveAsync(id, ReadIfMatch(), cancellationToken));

    /// <summary>PendingApproval -&gt; Draft with a reason. Coordinators only. Requires If-Match.</summary>
    [HttpPost("{id:guid}/reject")]
    [Authorize(Policy = AuthPolicies.Coordinator)]
    [ProducesResponseType<PolicySetResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<PolicySetResponse> Reject(Guid id, RejectPolicySetRequest request, CancellationToken cancellationToken)
        => WithETag(await policies.RejectAsync(id, request, ReadIfMatch(), cancellationToken));

    private PolicySetResponse WithETag(PolicySetResponse policy)
    {
        SetETag(policy.RowVersion);
        return policy;
    }
}
