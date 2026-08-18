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
}
