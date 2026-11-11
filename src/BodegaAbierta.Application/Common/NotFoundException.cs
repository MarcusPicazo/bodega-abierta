namespace BodegaAbierta.Application.Common;

/// <summary>The requested resource does not exist for the current food bank (HTTP 404).</summary>
public sealed class NotFoundException : Exception
{
    public NotFoundException(string resource, Guid id)
        : base($"{resource} '{id}' was not found.") { }

    public NotFoundException() : base("The resource was not found.") { }

    public NotFoundException(string message) : base(message) { }

    public NotFoundException(string message, Exception innerException) : base(message, innerException) { }
}
