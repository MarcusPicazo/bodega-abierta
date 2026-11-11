using BodegaAbierta.Application.Common;

namespace BodegaAbierta.Api.Http;

/// <summary>
/// Optimistic concurrency over HTTP: the entity version travels as a strong ETag on reads and must
/// come back in If-Match on writes.
/// </summary>
internal static class ETags
{
    public static string Format(string version) => $"\"{version}\"";

    /// <summary>Reads If-Match. Missing -&gt; 428 Precondition Required; malformed -&gt; 412 Precondition Failed.</summary>
    public static byte[] ParseIfMatch(HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var header = request.Headers.IfMatch.ToString().Trim();
        if (header.Length == 0)
        {
            throw new PreconditionRequiredException();
        }

        if (header.StartsWith("W/", StringComparison.Ordinal))
        {
            header = header[2..];
        }

        return VersionToken.TryDecode(header.Trim('"'), out var rowVersion)
            ? rowVersion
            : throw new PreconditionFailedException("The If-Match header is not a version issued by this API.");
    }
}

/// <summary>The request must carry If-Match (HTTP 428).</summary>
internal sealed class PreconditionRequiredException : Exception
{
    public PreconditionRequiredException()
        : base("This operation requires the If-Match header with the ETag from your last GET.") { }

    public PreconditionRequiredException(string message) : base(message) { }

    public PreconditionRequiredException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>The If-Match header does not match (HTTP 412).</summary>
internal sealed class PreconditionFailedException : Exception
{
    public PreconditionFailedException() : base("The If-Match precondition failed.") { }

    public PreconditionFailedException(string message) : base(message) { }

    public PreconditionFailedException(string message, Exception innerException) : base(message, innerException) { }
}
