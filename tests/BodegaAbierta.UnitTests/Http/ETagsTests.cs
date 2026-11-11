using BodegaAbierta.Api.Http;
using BodegaAbierta.Application.Common;
using Microsoft.AspNetCore.Http;

namespace BodegaAbierta.UnitTests.Http;

public sealed class ETagsTests
{
    private static HttpRequest RequestWithIfMatch(string? value)
    {
        var context = new DefaultHttpContext();
        if (value is not null)
        {
            context.Request.Headers.IfMatch = value;
        }

        return context.Request;
    }

    [Fact]
    public void ETag_round_trips_to_the_original_rowversion()
    {
        byte[] rowVersion = [0, 0, 0, 0, 0, 0, 0x07, 0xD1];
        var etag = ETags.Format(VersionToken.Encode(rowVersion));

        ETags.ParseIfMatch(RequestWithIfMatch(etag)).Should().Equal(rowVersion);
    }

    [Fact]
    public void Weak_etag_prefix_is_accepted()
    {
        byte[] rowVersion = [1, 2, 3, 4, 5, 6, 7, 8];

        ETags.ParseIfMatch(RequestWithIfMatch($"W/\"{VersionToken.Encode(rowVersion)}\"")).Should().Equal(rowVersion);
    }

    [Fact]
    public void Missing_if_match_requires_a_precondition()
    {
        var act = () => ETags.ParseIfMatch(RequestWithIfMatch(null));

        act.Should().Throw<PreconditionRequiredException>();
    }

    [Theory]
    [InlineData("\"not base64!\"")]
    [InlineData("*")]
    public void Malformed_if_match_fails_the_precondition(string header)
    {
        var act = () => ETags.ParseIfMatch(RequestWithIfMatch(header));

        act.Should().Throw<PreconditionFailedException>();
    }
}
