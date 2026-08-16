using NUnit.Framework;

namespace TheSpectre.ApiEnvelope.Tests;

[TestFixture]
public sealed class PagedResultTests
{
    [Test]
    public void PageCount_WithRowCountAnExactMultipleOfPageSize_DividesEvenly()
    {
        var pagination = new PaginationData(1, 20, 40);

        Assert.That(pagination.PageCount, Is.EqualTo(2));
    }

    [Test]
    public void PageCount_WithRowCountNotAMultipleOfPageSize_RoundsUpForThePartialPage()
    {
        var pagination = new PaginationData(1, 20, 57);

        Assert.That(pagination.PageCount, Is.EqualTo(3));
    }

    [Test]
    public void PageCount_WithPageSizeZero_ReturnsZeroRatherThanThrowing()
    {
        var pagination = new PaginationData(1, 0, 57);

        Assert.That(pagination.PageCount, Is.EqualTo(0));
    }

    [Test]
    public void FirstAndLastRowOnPage_OnAMiddlePage_ReportTheCorrectRowIndexes()
    {
        var pagination = new PaginationData(2, 20, 57);

        Assert.That(pagination.FirstRowOnPage, Is.EqualTo(21));
        Assert.That(pagination.LastRowOnPage, Is.EqualTo(40));
    }

    [Test]
    public void FirstAndLastRowOnPage_OnTheLastPartialPage_ReportTheCorrectRowIndexes()
    {
        var pagination = new PaginationData(3, 20, 57);

        Assert.That(pagination.FirstRowOnPage, Is.EqualTo(41));
        Assert.That(pagination.LastRowOnPage, Is.EqualTo(57));
    }

    [Test]
    public void DerivedMembers_WithRowCountZero_AreAllZero()
    {
        var pagination = new PaginationData(1, 20, 0);

        Assert.That(pagination.PageCount, Is.EqualTo(0));
        Assert.That(pagination.FirstRowOnPage, Is.EqualTo(0));
        Assert.That(pagination.LastRowOnPage, Is.EqualTo(0));
    }

    [Test]
    public void GetPaged_OnEnumerable_ReturnsThePageAndMatchingPagination()
    {
        var source = Enumerable.Range(1, 57);

        var result = source.GetPaged(2, 20);

        Assert.That(result.Data, Has.Count.EqualTo(20));
        Assert.That(result.Data[0], Is.EqualTo(21));
        Assert.That(result.Pagination.CurrentPage, Is.EqualTo(2));
        Assert.That(result.Pagination.PageSize, Is.EqualTo(20));
        Assert.That(result.Pagination.RowCount, Is.EqualTo(57));
    }

    [Test]
    public void GetPaged_OnQueryable_MatchesTheEnumerableOverload()
    {
        var source = Enumerable.Range(1, 57);

        var enumerableResult = source.GetPaged(2, 20);
        var queryableResult = source.AsQueryable().GetPaged(2, 20);

        Assert.That(queryableResult.Data, Is.EqualTo(enumerableResult.Data));
        Assert.That(queryableResult.Pagination, Is.EqualTo(enumerableResult.Pagination));
    }
}
