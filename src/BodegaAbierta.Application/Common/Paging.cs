using Microsoft.EntityFrameworkCore;

namespace BodegaAbierta.Application.Common;

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);

public static class Paging
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    /// <summary>Clamps paging input instead of rejecting it: page &gt;= 1, 1 &lt;= pageSize &lt;= 100.</summary>
    public static (int Page, int PageSize) Normalize(int page, int pageSize)
        => (Math.Max(page, 1), pageSize < 1 ? DefaultPageSize : Math.Min(pageSize, MaxPageSize));

    /// <summary>Runs COUNT and the page query. <paramref name="ordered"/> must already have a deterministic ORDER BY.</summary>
    public static async Task<PagedResult<TResult>> ToPagedResultAsync<TSource, TResult>(
        this IQueryable<TSource> ordered,
        int page,
        int pageSize,
        Func<TSource, TResult> map,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(map);
        (page, pageSize) = Normalize(page, pageSize);

        var total = await ordered.CountAsync(cancellationToken);
        var items = await ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<TResult>(items.Select(map).ToList(), page, pageSize, total);
    }
}
