using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using VersionFlow.Extensions.Results;
using VersionFlow.Extensions.Results.AspNetCore;
using Xunit;

namespace VersionFlow.Tests;

public sealed class ResultsTests
{
    [Fact]
    public void Outcomes_distinguish_null_and_default_success_from_failure()
    {
        Assert.True(TryResult.Succeeded().Success);
        Assert.True(TryResult<string?>.Succeeded(null).Success);
        Assert.True(TryResult<int>.Succeeded(0).Success);
        var exception = new InvalidOperationException("private diagnostics");
        var failure = TryResult<int>.Failed("Safe message", exception);
        Assert.False(failure.Success);
        Assert.Equal(0, failure.Data);
        Assert.Same(exception, failure.Exception);
        Assert.Throws<ArgumentException>(() => TryResult.Failed(" "));
        Assert.Throws<ArgumentException>(() => CursorTryResult<int>.Failed(""));
    }

    [Fact]
    public void Json_keeps_outcome_but_excludes_diagnostics()
    {
        var failure = TryResult<int>.Failed("Safe message", new Exception("secret"));
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(failure));
        Assert.False(json.RootElement.GetProperty("Success").GetBoolean());
        Assert.Equal("Safe message", json.RootElement.GetProperty("Message").GetString());
        Assert.False(json.RootElement.TryGetProperty("Exception", out _));
        Assert.DoesNotContain("secret", json.RootElement.GetRawText());
        using var page = JsonDocument.Parse(JsonSerializer.Serialize(PagedTryResult<int>.Failed("Unavailable")));
        Assert.Equal(JsonValueKind.Null, page.RootElement.GetProperty("PageNumber").ValueKind);
        Assert.Equal(0, page.RootElement.GetProperty("Data").GetArrayLength());
    }

    [Fact]
    public void Collections_are_snapshots_and_reject_null()
    {
        var source = new List<int> { 1, 2 };
        var result = CollectionTryResult<int>.Succeeded(source);
        source.Add(3);
        Assert.Equal(2, result.Count);
        Assert.Throws<NotSupportedException>(() => ((IList<int>)result.Data).Add(4));
        Assert.Throws<ArgumentNullException>(() => CollectionTryResult<int>.Succeeded(null!));
        Assert.Empty(CollectionTryResult<int>.Failed("Unavailable").Data);
    }

    [Fact]
    public void Numbered_pages_validate_windows_and_handle_large_totals()
    {
        var page = PagedTryResult<int>.Succeeded([3, 4], 2, 2, 5);
        Assert.Equal(3L, page.TotalPages);
        Assert.True(page.HasNextPage);
        Assert.True(page.HasPreviousPage);
        Assert.False(PagedTryResult<int>.Succeeded([5], 3, 2, 5).HasNextPage);
        Assert.Equal(0L, PagedTryResult<int>.Succeeded([], 1, 10, 0).TotalPages);
        Assert.False(PagedTryResult<int>.Succeeded([], 10, 10, 2).HasNextPage);
        Assert.Equal(long.MaxValue, PagedTryResult<int>.Succeeded([], 1, 1, long.MaxValue).TotalPages);
        Assert.Equal(long.MaxValue / 2 + 1, PagedTryResult<int>.Succeeded([], 1, 2, long.MaxValue).TotalPages);
        Assert.Throws<ArgumentOutOfRangeException>(() => PagedTryResult<int>.Succeeded([], 0, 10, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => PagedTryResult<int>.Succeeded([], 1, 0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => PagedTryResult<int>.Succeeded([], 1, 1, -1));
        Assert.Throws<ArgumentException>(() => PagedTryResult<int>.Succeeded([1, 2], 1, 1, 10));
        Assert.Throws<ArgumentException>(() => PagedTryResult<int>.Succeeded([1], 2, 10, 10));
    }

    [Fact]
    public void Offsets_advance_by_actual_count_without_overflow_or_empty_loops()
    {
        var page = OffsetTryResult<int>.Succeeded([3, 4], 2, 5, 10);
        Assert.Equal(4L, page.NextOffset);
        Assert.Null(OffsetTryResult<int>.Succeeded([], 2, 5, 10).NextOffset);
        Assert.Null(OffsetTryResult<int>.Succeeded([1], long.MaxValue - 1, 1, long.MaxValue).NextOffset);
        Assert.Null(OffsetTryResult<int>.Succeeded([], long.MaxValue, 1, 0).NextOffset);
        Assert.Throws<ArgumentOutOfRangeException>(() => OffsetTryResult<int>.Succeeded([], -1, 1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => OffsetTryResult<int>.Succeeded([], 0, 0, 0));
        Assert.Throws<ArgumentException>(() => OffsetTryResult<int>.Succeeded([1], 10, 1, 10));
    }

    [Fact]
    public void Cursor_completion_is_explicit_even_for_empty_pages()
    {
        Assert.True(CursorTryResult<int>.Succeeded([], "next").HasMore);
        Assert.False(CursorTryResult<int>.Succeeded([1]).HasMore);
        Assert.False(CursorTryResult<int>.Failed("Unavailable").HasMore);
        Assert.Throws<ArgumentException>(() => CursorTryResult<int>.Succeeded([], " "));
    }

    [Fact]
    public void Timestamp_cursors_preserve_ticks_and_normalize_offsets()
    {
        var value = new DateTimeOffset(2026, 11, 14, 12, 0, 0, TimeSpan.FromHours(-5)).AddTicks(1234567);
        var token = CursorTime.FormatUtc(value);
        Assert.Equal("2026-11-14T17:00:00.1234567Z", token);
        Assert.True(CursorTime.TryParse(token, out var parsed));
        Assert.Equal(value, parsed);
        Assert.Equal(TimeSpan.Zero, parsed.Offset);
        Assert.Throws<ArgumentException>(() => CursorTime.FormatUtc(new DateTime(2026, 11, 14)));
        var options = new JsonSerializerOptions { Converters = { new CursorTimeJsonConverter() } };
        Assert.Equal(value, JsonSerializer.Deserialize<DateTimeOffset>(JsonSerializer.Serialize(value, options), options));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<DateTimeOffset>("123", options));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<DateTimeOffset>("\"bad\"", options));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("2026-11-14T17:00:00Z")]
    [InlineData("2026-11-14T17:00:00.123456Z")]
    [InlineData("2026-11-14T17:00:00.1234567+00:00")]
    [InlineData("2026-11-14T17:00:00.1234567z")]
    public void Timestamp_cursors_reject_noncanonical_input(string? input)
        => Assert.False(CursorTime.TryParse(input, out _));

    [Fact]
    public async Task Http_keeps_empty_page_metadata_and_returns_safe_problem_details()
    {
        var (status, body) = await Execute(CursorTryResult<int>.Succeeded([], "next").AsHttpResult());
        Assert.Equal(200, status);
        using var json = JsonDocument.Parse(body);
        Assert.Equal("next", json.RootElement.GetProperty("nextCursor").GetString());
        Assert.Empty(json.RootElement.GetProperty("data").EnumerateArray());
        var numbered = await Execute(PagedTryResult<int>.Succeeded([], 1, 10, 0).AsHttpResult());
        Assert.Contains("\"totalPages\":0", numbered.Body);
        var failure = TryResult<int>.Failed("Internal message", new Exception("secret"));
        var defaultProblem = await Execute(failure.AsHttpResult());
        Assert.Equal(500, defaultProblem.Status);
        Assert.DoesNotContain("Internal", defaultProblem.Body);
        Assert.DoesNotContain("secret", defaultProblem.Body);
        var custom = await Execute(failure.AsHttpResult(404, "Not found", "Safe detail"));
        Assert.Equal(404, custom.Status);
        Assert.Contains("Safe detail", custom.Body);
        Assert.Contains("Not found", custom.Body);
    }

    [Fact]
    public async Task Http_distinguishes_no_content_from_empty_collection()
    {
        Assert.Equal((204, ""), await Execute(TryResult.Succeeded().AsHttpResult()));
        Assert.Equal((204, ""), await Execute(TryResult<string?>.Succeeded(null).AsHttpResult()));
        Assert.Equal((200, "[]"), await Execute(TryResult<int[]>.Succeeded([]).AsHttpResult()));
        Assert.Throws<ArgumentOutOfRangeException>(() => TryResult.Failed("Bad").AsHttpResult(200));
        Assert.Throws<ArgumentNullException>(() => ((TryResult)null!).AsHttpResult());
    }

    private static async Task<(int Status, string Body)> Execute(IResult result)
    {
        using var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = services };
        using var stream = new MemoryStream();
        context.Response.Body = stream;
        await result.ExecuteAsync(context);
        stream.Position = 0;
        return (context.Response.StatusCode, await new StreamReader(stream).ReadToEndAsync());
    }
}
