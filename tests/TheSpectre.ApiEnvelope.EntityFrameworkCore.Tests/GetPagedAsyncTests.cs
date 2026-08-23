using NUnit.Framework;

namespace TheSpectre.ApiEnvelope.EntityFrameworkCore.Tests;

[TestFixture]
public sealed class GetPagedAsyncTests
{
    [Test]
    public async Task GetPagedAsync_ReturnsTheRequestedPageAndItsMetadata()
    {
        using var fixture = new SqliteFixture(rowCount: 57);

        var result = await fixture.OrderedRows.GetPagedAsync(page: 2, pageSize: 20);

        Assert.Multiple(() =>
        {
            Assert.That(result.Data.Select(r => r.Id), Is.EqualTo(Enumerable.Range(21, 20)));
            Assert.That(result.Pagination.CurrentPage, Is.EqualTo(2));
            Assert.That(result.Pagination.PageSize, Is.EqualTo(20));
            Assert.That(result.Pagination.RowCount, Is.EqualTo(57));
            Assert.That(result.Pagination.PageCount, Is.EqualTo(3));
            Assert.That(result.Pagination.FirstRowOnPage, Is.EqualTo(21));
            Assert.That(result.Pagination.LastRowOnPage, Is.EqualTo(40));
        });
    }

    // rowCount, page, pageSize — first page, middle page, last partial page, past the end,
    // and an empty source. Paging bugs live at the boundaries, not in the middle.
    private static readonly object[] ParityCases =
    {
        new object[] { 57, 1, 20 },
        new object[] { 57, 2, 20 },
        new object[] { 57, 3, 20 },
        new object[] { 57, 4, 20 },
        new object[] { 0, 1, 20 },
        new object[] { 20, 1, 20 },
    };

    [TestCaseSource(nameof(ParityCases))]
    public async Task GetPagedAsync_MatchesTheSynchronousOverload(int rowCount, int page, int pageSize)
    {
        using var fixture = new SqliteFixture(rowCount);

        var expected = fixture.OrderedRows.GetPaged(page, pageSize);
        var actual = await fixture.OrderedRows.GetPagedAsync(page, pageSize);

        Assert.Multiple(() =>
        {
            // PagedResult<T> is a record whose Data property is IReadOnlyList<T>, so the
            // synthesised Equals compares the two lists by reference and would never match.
            // Compare the projection and the pagination record separately.
            Assert.That(actual.Data.Select(r => r.Id), Is.EqualTo(expected.Data.Select(r => r.Id)));
            Assert.That(actual.Pagination, Is.EqualTo(expected.Pagination));
        });
    }

    [Test]
    public void GetPagedAsync_PropagatesCancellation()
    {
        using var fixture = new SqliteFixture(rowCount: 57);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // CatchAsync here and ONLY here: EF Core may surface TaskCanceledException, which
        // derives from OperationCanceledException, and ThrowsAsync requires an exact type
        // match. The other two tests in this file use ThrowsAsync — accepting derived types
        // is a real cost, paid only where a derived type is genuinely expected.
        Assert.CatchAsync<OperationCanceledException>(
            () => fixture.OrderedRows.GetPagedAsync(page: 1, pageSize: 20, cts.Token));
    }

    [Test]
    public void GetPagedAsync_ThrowsOnAQueryThatIsNotBackedByEntityFrameworkCore()
    {
        // Asserted rather than handled. Falling back to synchronous enumeration here would
        // silently reintroduce the blocking call this package exists to remove.
        //
        // ThrowsAsync, not CatchAsync: the exact type matters here. ObjectDisposedException
        // derives from InvalidOperationException, so accepting derived types would let a
        // prematurely-disposed context satisfy a test whose name promises to catch something
        // else entirely.
        var inMemory = new List<Row> { new() { Id = 1, Name = "Row 1" } }.AsQueryable();

        Assert.ThrowsAsync<InvalidOperationException>(
            () => inMemory.GetPagedAsync(page: 1, pageSize: 20));
    }

    [Test]
    public void GetPagedAsync_ThrowsOnANullQuery()
    {
        IQueryable<Row> query = null!;

        // ThrowsAsync: ArgumentNullException.ThrowIfNull throws exactly this type, so there is
        // no derived-type ambiguity to accommodate.
        Assert.ThrowsAsync<ArgumentNullException>(() => query.GetPagedAsync(page: 1, pageSize: 20));
    }
}
