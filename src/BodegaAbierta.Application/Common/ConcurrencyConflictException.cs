namespace BodegaAbierta.Application.Common;

/// <summary>
/// Someone else changed the same rows (lot stock, slot capacity) between our read and our write and
/// the client did not send a version, so there is nothing to "precondition" on: HTTP 409, retry.
/// </summary>
public sealed class ConcurrencyConflictException : Exception
{
    public ConcurrencyConflictException()
        : base("The inventory or pickup slot changed while your request was processed. Please retry.") { }

    public ConcurrencyConflictException(string message) : base(message) { }

    public ConcurrencyConflictException(string message, Exception innerException) : base(message, innerException) { }
}
