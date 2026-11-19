using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using VersionFlow.Extensions.Results;
using VersionFlow.Extensions.Results.AspNetCore;
using Xunit;

namespace VersionFlow.Tests;

public sealed class HttpResponseResultTests
{
    [Fact]
    public async Task Reads_payload_and_no_content_and_leaves_response_owned_by_caller()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new[] { 1, 2 }) };
        var result = await response.FromHttpResponseAsync<int[]>();
        Assert.True(result.IsSuccess);
        Assert.Equal(new[] { 1, 2 }, result.Data);
        Assert.NotEmpty(await response.Content.ReadAsStringAsync());
        using var empty = new HttpResponseMessage(HttpStatusCode.NoContent);
        Assert.True((await empty.FromHttpResponseAsync()).IsSuccess);
        var nullable = await empty.FromHttpResponseAsync<int?>();
        Assert.True(nullable.IsSuccess);
        Assert.Null(nullable.Data);
    }

    [Fact]
    public async Task Reads_all_collection_envelopes_with_metadata()
    {
        using var collection = Response(CollectionTryResult<int>.Succeeded([]));
        Assert.Empty((await collection.FromHttpCollectionAsync<int>()).Data);
        using var page = Response(PagedTryResult<int>.Succeeded([3], 2, 2, 3));
        var numbered = await page.FromHttpPageAsync<int>();
        Assert.True(numbered.IsSuccess);
        Assert.Equal(2, numbered.PageNumber);
        Assert.Equal(3, numbered.TotalCount);
        Assert.Equal(new[] { 3 }, numbered.Data);
        using var offset = Response(OffsetTryResult<int>.Succeeded([3], 2, 1, 4));
        Assert.Equal(3, (await offset.FromHttpOffsetAsync<int>()).NextOffset);
        using var cursor = Response(CursorTryResult<int>.Succeeded([], "next"));
        Assert.Equal("next", (await cursor.FromHttpCursorAsync<int>()).NextCursor);
    }

    [Theory]
    [InlineData("{\"detail\":\"Missing item\"}", "application/problem+json", "Missing item")]
    [InlineData("{\"title\":\"Not found\"}", "application/problem+json", "Not found")]
    [InlineData("<html>secret</html>", "text/html", "HTTP request failed with status 404.")]
    [InlineData("invalid", "application/problem+json", "HTTP request failed with status 404.")]
    public async Task Maps_failures(string body, string mediaType, string expected)
    {
        using var response = new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent(body, Encoding.UTF8, mediaType) };
        var result = await response.FromHttpResponseAsync<int>();
        Assert.False(result.IsSuccess);
        Assert.Equal(expected, result.Message);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"isSuccess\":true,\"data\":[]}")]
    [InlineData("{\"isSuccess\":true,\"data\":[1,2],\"pageNumber\":1,\"pageSize\":1,\"totalCount\":2}")]
    public async Task Rejects_invalid_page_envelopes(string body)
    {
        using var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        var result = await response.FromHttpPageAsync<int>();
        Assert.False(result.IsSuccess);
        Assert.IsType<JsonException>(result.Exception);
    }

    [Fact]
    public async Task Invalid_json_fails_and_cancellation_propagates()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("invalid") };
        Assert.False((await response.FromHttpResponseAsync<int>()).IsSuccess);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => response.FromHttpResponseAsync<int>(cancellationToken: cancellation.Token));
        await Assert.ThrowsAsync<ArgumentNullException>(() => ((HttpResponseMessage)null!).FromHttpResponseAsync());
    }

    private static HttpResponseMessage Response<T>(T value) => new(HttpStatusCode.OK) { Content = JsonContent.Create(value) };
}
