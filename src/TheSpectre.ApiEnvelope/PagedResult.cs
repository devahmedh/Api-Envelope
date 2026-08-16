namespace TheSpectre.ApiEnvelope;

/// <summary>A single page of results together with its page metadata.</summary>
/// <typeparam name="T">The row type.</typeparam>
/// <param name="Data">The rows on this page.</param>
/// <param name="Pagination">The page metadata.</param>
/// <remarks>
/// Placed inside the envelope's <c>result</c> slot, so a paged response reads as
/// <c>result.data</c> and <c>result.pagination</c>.
/// </remarks>
public sealed record PagedResult<T>(IReadOnlyList<T> Data, PaginationData Pagination);
