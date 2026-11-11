using System.Security.Claims;
using BodegaAbierta.Application.Abstractions;

namespace BodegaAbierta.Api.Auth;

/// <summary>Adapts the request's ClaimsPrincipal to the Application's <see cref="ICurrentUser"/> port.</summary>
internal sealed class HttpCurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    private ClaimsPrincipal? User => httpContextAccessor.HttpContext?.User;

    public string? UserId => User?.Identity?.IsAuthenticated == true
        ? User.FindFirstValue(ClaimTypes.NameIdentifier)
        : null;

    public bool IsInRole(string role) => User?.IsInRole(role) == true;
}

internal static class AuthPolicies
{
    public const string Coordinator = nameof(Coordinator);
}
