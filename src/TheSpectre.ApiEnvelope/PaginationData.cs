namespace TheSpectre.ApiEnvelope;

/// <summary>Page metadata accompanying a <see cref="PagedResult{T}"/>.</summary>
/// <param name="CurrentPage">The 1-based page number that was returned.</param>
/// <param name="PageSize">The maximum number of rows per page.</param>
/// <param name="RowCount">The total number of rows across all pages.</param>
/// <remarks>
/// The derived members are serialised alongside the three supplied values so the client
/// never has to compute them — recomputing <c>pageCount</c> in every frontend is a classic
/// source of off-by-one errors.
/// </remarks>
public sealed record PaginationData(int CurrentPage, int PageSize, int RowCount)
{
    /// <summary>The total number of pages, or 0 when <see cref="PageSize"/> is not positive.</summary>
    public int PageCount => PageSize <= 0 ? 0 : (int)Math.Ceiling(RowCount / (double)PageSize);

    /// <summary>The 1-based index of the first row on this page, or 0 when there are no rows.</summary>
    public int FirstRowOnPage => RowCount <= 0 ? 0 : ((CurrentPage - 1) * PageSize) + 1;

    /// <summary>The 1-based index of the last row on this page, or 0 when there are no rows.</summary>
    public int LastRowOnPage => RowCount <= 0 ? 0 : Math.Min(CurrentPage * PageSize, RowCount);
}
