using VersionFlow.Extensions.Railway;
using Xunit;

namespace VersionFlow.Tests;

public sealed class RailwayWhenTests
{
    [Fact]
    public void When_preserves_skipped_results_and_returns_selected_result()
    {
        var source = RailResult<int>.Passed(3);
        var failure = RailResult<int>.Failed("denied");
        Assert.Same(source, source.When(_ => false, _ => throw new Exception()));
        Assert.Same(failure, failure.When(_ => throw new Exception(), _ => throw new Exception()));
        var calls = 0;
        Assert.Same(failure, source.When(n => n == 3, n =>
        {
            calls++;
            Assert.Equal(3, n);
            return failure;
        }));
        Assert.Equal(1, calls);
        Assert.True(source.When(_ => true, _ => failure).Then<int>(_ => throw new Exception()).IsFailed);
        Assert.Equal(8, source.When(_ => true, n => RailResult<int>.Passed(n + 1)).Then(n => RailResult<int>.Passed(n * 2)).Value);
    }

    [Fact]
    public void When_validates_callbacks_and_captures_exceptions_except_cancellation()
    {
        var source = RailResult<int>.Passed(3);
        var error = new Exception("broken");
        Assert.Same(error, source.When(_ => throw error, _ => source).Errors[0].Exception);
        Assert.Same(error, source.When(_ => true, _ => throw error).Errors[0].Exception);
        Assert.IsType<InvalidOperationException>(source.When(_ => true, _ => null!).Errors[0].Exception);
        Assert.Throws<OperationCanceledException>(() => source.When(_ => true, _ => throw new OperationCanceledException()));
        var failure = RailResult<int>.Failed("denied");
        Assert.Throws<ArgumentNullException>(() => failure.When(null!, _ => source));
        Assert.Throws<ArgumentNullException>(() => failure.When(_ => false, null!));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Async_forms_preserve_skips_forward_tokens_and_capture_failures(bool asyncPredicate)
    {
        using var cts = new CancellationTokenSource();
        var source = RailResult<int>.Passed(3);
        var failure = RailResult<int>.Failed("denied");
        var predicateCalls = 0;
        Task<RailResult<int>> Run(RailResult<int> input, bool matches,
            Func<int, CancellationToken, Task<RailResult<int>>> operation) =>
            asyncPredicate
                ? input.WhenAsync(async (n, ct) =>
                {
                    await Task.Yield();
                    predicateCalls++;
                    Assert.Equal(cts.Token, ct);
                    return matches;
                }, operation, cts.Token)
                : input.WhenAsync(n => { predicateCalls++; return matches; }, operation, cts.Token);
        Assert.Same(failure, await Run(failure, true, (_, _) => throw new Exception()));
        Assert.Equal(0, predicateCalls);
        Assert.Same(source, await Run(source, false, (_, _) => throw new Exception()));
        Assert.Same(failure, await Run(source, true, async (n, ct) =>
        {
            await Task.Yield();
            Assert.Equal(3, n);
            Assert.Equal(cts.Token, ct);
            return failure;
        }));
        var error = new Exception("broken");
        Assert.Same(error, (await Run(source, true, (_, _) => Task.FromException<RailResult<int>>(error))).Errors[0].Exception);
        Assert.IsType<InvalidOperationException>((await Run(source, true, (_, _) => Task.FromResult<RailResult<int>>(null!))).Errors[0].Exception);
        await Assert.ThrowsAsync<OperationCanceledException>(() => Run(source, true, (_, _) => throw new OperationCanceledException()));
        await Assert.ThrowsAsync<ArgumentNullException>(() => Run(failure, false, null!));
        cts.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => Run(failure, false, (_, _) => Task.FromResult(source)));
    }

    [Fact]
    public async Task Task_extensions_compose_and_preserve_source_contract()
    {
        var source = RailResult<int>.Passed(3);
        var result = await Task.FromResult(source)
            .When(_ => true, n => RailResult<int>.Passed(n + 1))
            .WhenAsync(_ => true, (n, _) => Task.FromResult(RailResult<int>.Passed(n * 2)))
            .WhenAsync((_, _) => Task.FromResult(false), (_, _) => throw new Exception())
            .Then(n => RailResult<string>.Passed(n.ToString()));
        Assert.Equal("8", result.Value);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Task.FromResult<RailResult<int>>(null!).When(_ => true, _ => source));
        var error = new Exception("upstream");
        Assert.Same(error, await Assert.ThrowsAsync<Exception>(() =>
            Task.FromException<RailResult<int>>(error).When(_ => true, _ => source)));
        using var cts = new CancellationTokenSource();
        var pending = new TaskCompletionSource<RailResult<int>>();
        var waiting = pending.Task.WhenAsync(_ => true, (_, _) => Task.FromResult(source), cts.Token);
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting);
        Assert.False(pending.Task.IsCompleted);
    }
}
