using BodegaAbierta.Api.Http;
using Microsoft.AspNetCore.Mvc;

namespace BodegaAbierta.Api.Controllers;

/// <summary>
/// Thin HTTP adapters: translate HTTP (route, query, headers) to a service call and back.
/// No business logic lives in controllers.
/// </summary>
[ApiController]
[Produces("application/json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
public abstract class ApiControllerBase : ControllerBase
{
    protected void SetETag(string version) => Response.Headers.ETag = ETags.Format(version);

    protected byte[] ReadIfMatch() => ETags.ParseIfMatch(Request);

    /// <summary>201 Created with Location = current collection path + id.</summary>
    protected CreatedResult CreatedAtCollection<T>(Guid id, T body)
        => Created($"{Request.PathBase}{Request.Path.Value?.TrimEnd('/')}/{id}", body);
}
