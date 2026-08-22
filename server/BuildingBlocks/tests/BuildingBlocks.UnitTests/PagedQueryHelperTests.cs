using BuildingBlocks.Application.Queries;

namespace BuildingBlocks.UnitTests;

public class PagedQueryHelperTests
{
    private sealed record PagedQuery(int? Page, int? PerPage) : IPagedQuery;


    // ---------------------------------------------------------------
    // GetPageData
    // ---------------------------------------------------------------

    [Fact]
    public void GetPageData_WhenPageAndPerPageProvided_OffsetsByPreviousPages()
    {
        var query = new PagedQuery(Page: 3, PerPage: 10);

        var pageData = PagedQueryHelper.GetPageData(query);

        pageData.Offset.Should().Be(20, "page 3 with 10 per page skips the first two pages");
        pageData.Next.Should().Be(10);
    }

    [Fact]
    public void GetPageData_WhenOnFirstPage_OffsetsByZero()
    {
        var query = new PagedQuery(Page: 1, PerPage: 20);

        var pageData = PagedQueryHelper.GetPageData(query);

        pageData.Offset.Should().Be(0);
        pageData.Next.Should().Be(20);
    }

    [Fact]
    public void GetPageData_WhenPageMissing_ReturnsZeroOffset()
    {
        var query = new PagedQuery(Page: null, PerPage: 20);

        var pageData = PagedQueryHelper.GetPageData(query);

        pageData.Offset.Should().Be(0, "paging is disabled so all rows are read from the start");
        pageData.Next.Should().Be(20);
    }

    [Fact]
    public void GetPageData_WhenPerPageMissing_ReturnsMaxValueNext()
    {
        var query = new PagedQuery(Page: 2, PerPage: null);

        var pageData = PagedQueryHelper.GetPageData(query);

        pageData.Offset.Should().Be(0, "offset requires both Page and PerPage");
        pageData.Next.Should().Be(int.MaxValue, "without a page size every remaining row is returned");
    }

    // ---------------------------------------------------------------
    // AppendPgPageStatement / AppendPageStatement
    // ---------------------------------------------------------------

    [Fact]
    public void AppendPgPageStatement_WhenCalled_AppendsLimitOffsetSuffix()
    {
        var result = PagedQueryHelper.AppendPgPageStatement("SELECT * FROM users");

        result.Should().Be("SELECT * FROM users LIMIT @Next OFFSET @Offset;");
    }

    [Fact]
    public void AppendPageStatement_WhenCalled_AppendsOffsetFetchSuffix()
    {
        var result = PagedQueryHelper.AppendPageStatement("SELECT * FROM users");

        result.Should().Be("SELECT * FROM users OFFSET @Offset ROWS FETCH NEXT @Next ROWS ONLY; ");
    }
}
