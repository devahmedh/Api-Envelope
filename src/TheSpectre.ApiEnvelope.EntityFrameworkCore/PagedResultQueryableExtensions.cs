using Microsoft.EntityFrameworkCore;

namespace TheSpectre.ApiEnvelope;

/// <summary>Asynchronous paging helpers for Entity Framework Core queries.</summary>
public static class PagedResultQueryableExtensions
{
    /// <summary>Returns one page of <paramref name="query"/>, executed asynchronously.</summary>
    /// <typeparam name="T">The row type.</typeparam>
    /// <param name="query">The query to page. Must be backed by an Entity Framework Core provider.</param>
    /// <param name="page">The 1-based page number.</param>
    /// <param name="pageSize">The maximum rows per page.</param>
    /// <param name="cancellationToken">Cancels both round-trips.</param>
    /// <remarks>
    /// <para>
    /// Two round-trips: the count, then the page. This is the asynchronous counterpart to
    /// <see cref="PagedResultExtensions.GetPaged{T}(IQueryable{T}, int, int)"/> and returns an
    /// identical <see cref="PagedResult{T}"/> for the same data.
    /// </para>
    /// <para>
    /// The query must come from an Entity Framework Core provider. Passing an in-memory
    /// sequence's <see cref="IQueryable{T}"/> throws <see cref="InvalidOperationException"/>;
    /// use the synchronous overload for those.
    /// </para>
    /// </remarks>
    public static async Task<PagedResult<T>> GetPagedAsync<T>(
        this IQueryable<T> query,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var count = await query
            .CountAsync(cancellationToken)
            .ConfigureAwait(false);

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new PagedResult<T>(items, new PaginationData(page, pageSize, count));
    }
}
