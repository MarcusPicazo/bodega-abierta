using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace BodegaAbierta.Api.Auth;

/// <summary>
/// DEVELOPMENT/TESTING ONLY stand-in for real authentication (JWT/OIDC comes later):
/// builds the user from the <c>X-User-Id</c> and <c>X-User-Roles</c> (comma-separated) headers.
/// In any other environment it never authenticates anyone, so [Authorize] endpoints return 401.
/// </summary>
internal sealed class DevHeaderAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IHostEnvironment environment)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "DevHeader";
    public const string UserIdHeader = "X-User-Id";
    public const string RolesHeader = "X-User-Roles";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!(environment.IsDevelopment() || environment.IsEnvironment("Testing")))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var userId = Request.Headers[UserIdHeader].ToString().Trim();
        if (userId.Length == 0)
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, userId), new(ClaimTypes.Name, userId) };
        claims.AddRange(Request.Headers[RolesHeader].ToString()
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(role => new Claim(ClaimTypes.Role, role)));

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName));
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName)));
    }
}
