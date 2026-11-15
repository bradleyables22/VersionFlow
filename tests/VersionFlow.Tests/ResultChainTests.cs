using VersionFlow.Extensions.Results;
using Xunit;

namespace VersionFlow.Tests;

public sealed class ResultChainTests
{
    [Fact]
    public void Conversions_preserve_factory_semantics()
    {
        Assert.True(((string?)null).ToTryResult().Success);
        var values = new List<int> { 1, 2 };
        var collection = values.ToCollectionTryResult();
        values.Add(3);
        Assert.Equal(2, collection.Count);
        Assert.Equal(2L, values.ToPagedTryResult(1, 3, 6).TotalPages);
        Assert.Equal(3L, values.ToOffsetTryResult(0, 3, 6).NextOffset);
        Assert.Equal("next", values.ToCursorTryResult("next").NextCursor);
        Assert.Throws<ArgumentOutOfRangeException>(() => values.ToPagedTryResult(0, 3, 6));
        Assert.Throws<ArgumentException>(() => values.ToOffsetTryResult(0, 1, 6));
        Assert.Throws<ArgumentException>(() => values.ToCursorTryResult(" "));
    }

    [Fact]
    public void Checks_preserve_success_or_first_failure_and_handle_null()
    {
        var original = 42.ToTryResult();
        Assert.Same(original, original.FailWhen(_ => false, "Unused"));
        var failed = original.FailWhen(n => n == 42, "First");
        Assert.False(failed.Success);
        Assert.Equal(0, failed.Data);
        Assert.Equal("First", failed.Message);
        Assert.Same(failed, failed.FailWhen(_ => throw new Exception(), "Second"));
        var diagnostic = TryResult<int>.Failed("Original", new Exception());
        Assert.Same(diagnostic, diagnostic.FailWhen(_ => true, "Replacement"));
        Assert.False(((string?)null).ToTryResult().FailWhen(s => s is null, "Missing").Success);
        Assert.Throws<ArgumentException>(() => original.FailWhen(_ => false, " "));
        Assert.Throws<ArgumentNullException>(() => original.FailWhen(null!, "Missing"));
        Assert.Throws<InvalidOperationException>(() => original.FailWhen(_ => throw new InvalidOperationException(), "Rule"));
    }

    [Fact]
    public async Task Async_checks_chain_and_skip_after_first_failure()
    {
        var original = 42.ToTryResult();
        Assert.Same(original, await original.FailWhenAsync(_ => Task.FromResult(false), "Unused"));
        using var cancellation = new CancellationTokenSource();
        var failed = await original
            .FailWhenAsync(async (n, token) =>
            {
                Assert.Equal(cancellation.Token, token);
                await Task.Yield();
                return n == 42;
            }, "First", cancellation.Token)
            .FailWhen(_ => throw new Exception(), "Second")
            .FailWhenAsync(_ => throw new Exception(), "Third")
            .FailWhenAsync((_, _) => throw new Exception(), "Fourth", cancellation.Token);
        Assert.False(failed.Success);
        Assert.Equal("First", failed.Message);
        Assert.Same(failed, await failed.FailWhenAsync(_ => throw new Exception(), "Skipped"));
        Assert.False((await ((string?)null).ToTryResult()
            .FailWhenAsync(s => Task.FromResult(s is null), "Missing")).Success);
    }

    [Fact]
    public async Task Async_checks_propagate_exceptions_and_cancellation()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => 1.ToTryResult()
            .FailWhenAsync(_ => Task.FromException<bool>(new InvalidOperationException()), "Rule"));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => 1.ToTryResult()
            .FailWhenAsync((_, _) => throw new Exception("Must skip"), "Rule", cancellation.Token));
        var pending = new TaskCompletionSource<TryResult<int>>();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending.Task
            .FailWhenAsync((_, _) => Task.FromResult(false), "Rule", cancellation.Token));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Task.FromException<TryResult<int>>(new InvalidOperationException())
            .FailWhen(_ => false, "Rule"));
    }
}
