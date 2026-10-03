using Shelter.BuildingBlocks.Persistence.Paging;

namespace Shelter.BuildingBlocks.Tests.Persistence;

public sealed class PageRequestTests
{
    [Fact]
    public void Defaults_to_the_first_page_of_25()
    {
        var page = new PageRequest();
        Assert.Equal((1, 25, 0), (page.CurrentPage, page.Size, page.Skip));
    }

    [Theory]
    [InlineData(0, 0, 1, 1, 0)]
    [InlineData(-3, 500, 1, 100, 0)]
    [InlineData(3, 10, 3, 10, 20)]
    [InlineData(int.MaxValue, 100, int.MaxValue, 100, int.MaxValue)]
    public void Clamps_out_of_range_values(int requestedPage, int requestedSize, int page, int size, int skip)
    {
        var request = new PageRequest(requestedPage, requestedSize);
        Assert.Equal((page, size, skip), (request.CurrentPage, request.Size, request.Skip));
    }
}
