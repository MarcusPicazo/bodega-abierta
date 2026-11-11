namespace BodegaAbierta.Application.Common;

/// <summary>Converts a SQL Server rowversion to the opaque string clients see (body "version" and ETag).</summary>
public static class VersionToken
{
    public static string Encode(byte[] rowVersion) => Convert.ToBase64String(rowVersion);

    public static bool TryDecode(string? token, out byte[] rowVersion)
    {
        rowVersion = [];
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        var buffer = new byte[token.Length];
        if (!Convert.TryFromBase64String(token, buffer, out var written) || written == 0)
        {
            return false;
        }

        rowVersion = buffer[..written];
        return true;
    }
}
