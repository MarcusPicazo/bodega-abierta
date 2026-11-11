namespace BodegaAbierta.Application.Abstractions;

/// <summary>Who is performing the current operation (the "actor" written to audit entries and approvals).</summary>
public interface ICurrentUser
{
    /// <summary>Stable user identifier, or <c>null</c> when the request is anonymous.</summary>
    string? UserId { get; }

    bool IsInRole(string role);
}

public static class Roles
{
    /// <summary>Approves or rejects policy sets and reads the audit log.</summary>
    public const string Coordinator = "Coordinator";
}

public static class CurrentUserExtensions
{
    public static string RequireUserId(this ICurrentUser user)
    {
        ArgumentNullException.ThrowIfNull(user);
        return user.UserId ?? throw new UnauthenticatedException();
    }

    public static string RequireRole(this ICurrentUser user, string role)
    {
        var userId = user.RequireUserId();
        return user.IsInRole(role) ? userId : throw new ForbiddenException($"This operation requires the {role} role.");
    }
}

/// <summary>No authenticated user (HTTP 401).</summary>
public sealed class UnauthenticatedException : Exception
{
    public UnauthenticatedException() : base("This operation requires an authenticated user.") { }

    public UnauthenticatedException(string message) : base(message) { }

    public UnauthenticatedException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>Authenticated, but not allowed (HTTP 403).</summary>
public sealed class ForbiddenException : Exception
{
    public ForbiddenException() : base("You are not allowed to perform this operation.") { }

    public ForbiddenException(string message) : base(message) { }

    public ForbiddenException(string message, Exception innerException) : base(message, innerException) { }
}
