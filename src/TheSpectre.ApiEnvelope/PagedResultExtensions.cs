namespace TheSpectre.ApiEnvelope;

/// <summary>Paging helpers producing a <see cref="PagedResult{T}"/>.</summary>
public static class PagedResultExtensions
{
    /// <summary>Returns one page of <paramref name="query"/>.</summary>
    /// <typeparam name="T">The row type.</typeparam>
    /// <param name="query">The query to page.</param>
    /// <param name="page">The 1-based page number.</param>
    /// <param name="pageSize">The maximum rows per page.</param>
    /// <remarks>
    /// <para>
    /// <b>This overload executes synchronously.</b> Against a database provider it blocks the
    /// calling thread across two round-trips — the count and the page — which is why an async
    /// alternative belongs in a provider-specific package. This library takes no dependency on
    /// Entity Framework Core, so it cannot offer <c>CountAsync</c>/<c>ToListAsync</c> here.
    /// Prefer performing the query asynchronously yourself and constructing
    /// <see cref="PagedResult{T}"/> directly when the source is a database.
    /// </para>
    /// </remarks>
    public static PagedResult<T> GetPaged<T>(this IQueryable<T> query, int page, int pageSize)
    {
        ArgumentNullException.ThrowIfNull(query);

        var count = query.Count();
        var items = query.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return new PagedResult<T>(items, new PaginationData(page, pageSize, count));
    }

    /// <summary>Returns one page of <paramref name="source"/>.</summary>
    /// <typeparam name="T">The row type.</typeparam>
    /// <param name="source">The sequence to page.</param>
    /// <param name="page">The 1-based page number.</param>
    /// <param name="pageSize">The maximum rows per page.</param>
    public static PagedResult<T> GetPaged<T>(this IEnumerable<T> source, int page, int pageSize)
    {
        ArgumentNullException.ThrowIfNull(source);

        var materialised = source as IReadOnlyCollection<T> ?? source.ToList();
        var items = materialised.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return new PagedResult<T>(items, new PaginationData(page, pageSize, materialised.Count));
    }
}
