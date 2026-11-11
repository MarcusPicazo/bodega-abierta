using Asp.Versioning;
using BodegaAbierta.Api.Auth;
using BodegaAbierta.Application.Auditing;
using BodegaAbierta.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BodegaAbierta.Api.Controllers.V1;

[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/audit-entries")]
[Authorize(Policy = AuthPolicies.Coordinator)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
public sealed class AuditEntriesController(AuditEntryService audit) : ApiControllerBase
{
    /// <summary>Newest first. Filter by entityType (e.g. "Donor") and entityId.</summary>
    [HttpGet]
    public Task<PagedResult<AuditEntryResponse>> List([FromQuery] ListAuditEntriesQuery query, CancellationToken cancellationToken)
        => audit.ListAsync(query, cancellationToken);
}
