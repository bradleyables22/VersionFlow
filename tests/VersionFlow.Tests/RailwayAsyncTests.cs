using VersionFlow.Extensions.Railway;
using Xunit;

namespace VersionFlow.Tests;

public sealed class RailwayAsyncTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Validation_recovers_once_and_rechecks(bool expected)
    {
        using var cts = new CancellationTokenSource();
        var checks = 0;
        var repairs = 0;
        async Task<bool> Predicate(int n, CancellationToken ct)
        {
            await Task.Yield();
            Assert.Equal(cts.Token, ct);
            checks++;
            return n == 2 ? expected : !expected;
        }
        async Task<int> Recover(int n, CancellationToken ct)
        {
            await Task.Yield();
            Assert.Equal(cts.Token, ct);
            Assert.Equal(1, n);
            repairs++;
            return 2;
        }
        var start = RailResult<int>.Passed(1);
        var result = expected
            ? await start.EnsureTrueAsync(Predicate, recover: Recover, cancellationToken: cts.Token)
            : await start.EnsureFalseAsync(Predicate, recover: Recover, cancellationToken: cts.Token);
        Assert.Equal(2, result.Value);
        Assert.Equal(2, checks);
        Assert.Equal(1, repairs);
        Assert.Same(result, await result.EnsureAsync((_, _) => Task.FromResult(true),
            recover: (_, _) => throw new Exception()));
    }

    [Fact]
    public async Task Validation_preserves_errors_and_recovery_details()
    {
        var start = RailResult<int>.Passed(1);
        var error = new RailError("invalid", new RailErrorCode("custom"));
        var exception = new Exception("broken");
        var failed = await start.EnsureAsync((_, _) => Task.FromResult(false), error);
        Assert.Equal(error, failed.Errors[0]);
        Assert.Same(failed, await failed.EnsureFalseAsync((_, _) => throw new Exception(),
            recover: (_, _) => throw new Exception()));
        var initial = await start.EnsureTrueAsync((_, _) => Task.FromException<bool>(exception), error,
            recover: (_, _) => throw new Exception("Must skip recovery"));
        Assert.Equal(error.Code, initial.Errors[0].Code);
        Assert.Same(exception, initial.Errors[0].Exception);
        Assert.Null(initial.Errors[0].RecoveryFailure);
        var uncorrected = await start.EnsureFalseAsync((_, _) => Task.FromResult(true), error,
            (_, _) => Task.FromResult(2));
        Assert.Equal(error.Code, uncorrected.Errors[0].Code);
        Assert.NotNull(uncorrected.Errors[0].RecoveryFailure);
        var recovery = await start.EnsureTrueAsync((_, _) => Task.FromResult(false), error,
            (_, _) => Task.FromException<int>(exception));
        Assert.Same(exception, recovery.Errors[0].RecoveryFailure!.Value.Exception);
        var recheck = await start.EnsureTrueAsync((n, _) => n == 1 ? Task.FromResult(false) : Task.FromException<bool>(exception),
            error, (_, _) => Task.FromResult(2));
        Assert.Same(exception, recheck.Errors[0].RecoveryFailure!.Value.Exception);
        await Assert.ThrowsAsync<OperationCanceledException>(() => start.EnsureAsync(
            (_, _) => throw new OperationCanceledException()));
        await Assert.ThrowsAsync<OperationCanceledException>(() => start.EnsureAsync(
            (_, _) => Task.FromResult(false), recover: (_, _) => throw new OperationCanceledException()));
        await Assert.ThrowsAsync<OperationCanceledException>(() => start.EnsureAsync(
            (n, _) => n == 1 ? Task.FromResult(false) : throw new OperationCanceledException(),
            recover: (_, _) => Task.FromResult(2)));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Observers_and_match_await_only_the_matching_outcome(bool passed)
    {
        using var cts = new CancellationTokenSource();
        var result = passed ? RailResult<int>.Passed(7) : RailResult<int>.Failed("bad");
        var calls = 0;
        async Task Observe(CancellationToken ct)
        {
            await Task.Yield();
            Assert.Equal(cts.Token, ct);
            calls++;
        }
        Assert.Same(result, await result.OnSuccessAsync((n, ct) =>
        {
            Assert.True(passed);
            Assert.Equal(7, n);
            return Observe(ct);
        }, cts.Token));
        Assert.Same(result, await result.OnFailureAsync((errors, ct) =>
        {
            Assert.False(passed);
            Assert.Same(result.Errors, errors);
            return Observe(ct);
        }, cts.Token));
        Assert.Equal(1, calls);
        var output = await result.MatchAsync(async (n, ct) =>
        {
            Assert.True(passed);
            await Observe(ct);
            return n.ToString();
        }, async (errors, ct) =>
        {
            Assert.False(passed);
            await Observe(ct);
            return errors[0].Message;
        }, cts.Token);
        Assert.Equal(passed ? "7" : "bad", output);
        Assert.Equal(2, calls);
        var exception = new InvalidOperationException("observer");
        Assert.Same(exception, await Assert.ThrowsAsync<InvalidOperationException>(() => passed
            ? result.OnSuccessAsync((_, _) => Task.FromException(exception))
            : result.OnFailureAsync((_, _) => Task.FromException(exception))));
        Assert.Same(exception, await Assert.ThrowsAsync<InvalidOperationException>(() =>
            result.MatchAsync<int>((_, _) => Task.FromException<int>(exception), (_, _) => Task.FromException<int>(exception))));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Async_predicate_selects_one_branch(bool condition)
    {
        using var cts = new CancellationTokenSource();
        var expected = RailResult<string>.Passed("selected");
        var result = await RailResult<int>.Passed(7).BranchAsync(async (n, ct) =>
        {
            await Task.Yield();
            Assert.Equal(7, n);
            Assert.Equal(cts.Token, ct);
            return condition;
        }, (n, ct) =>
        {
            Assert.True(condition);
            Assert.Equal(cts.Token, ct);
            return Task.FromResult(expected);
        }, (n, ct) =>
        {
            Assert.False(condition);
            Assert.Equal(cts.Token, ct);
            return Task.FromResult(expected);
        }, cts.Token);
        Assert.Same(expected, result);
        var failed = RailResult<int>.Failed("original");
        Assert.Equal(failed.Errors, (await failed.BranchAsync<int>((_, _) => throw new Exception(),
            (_, _) => throw new Exception(), (_, _) => throw new Exception())).Errors);
        var exception = new Exception("predicate");
        Assert.Same(exception, (await result.BranchAsync<int>((_, _) => Task.FromException<bool>(exception),
            (_, _) => throw new Exception(), (_, _) => throw new Exception())).Errors[0].Exception);
        await Assert.ThrowsAsync<OperationCanceledException>(() => result.BranchAsync<int>(
            (_, _) => throw new OperationCanceledException(), (_, _) => throw new Exception(), (_, _) => throw new Exception()));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Precancelled_tokens_and_invalid_arguments_are_not_skipped(bool passed)
    {
        var result = passed ? RailResult<int>.Passed(1) : RailResult<int>.Failed("bad");
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => result.EnsureAsync(
            (_, _) => throw new Exception(), cancellationToken: cts.Token));
        await Assert.ThrowsAsync<OperationCanceledException>(() => result.OnSuccessAsync(
            (_, _) => throw new Exception(), cts.Token));
        await Assert.ThrowsAsync<OperationCanceledException>(() => result.OnFailureAsync(
            (_, _) => throw new Exception(), cts.Token));
        await Assert.ThrowsAsync<OperationCanceledException>(() => result.MatchAsync<int>(
            (_, _) => throw new Exception(), (_, _) => throw new Exception(), cts.Token));
        await Assert.ThrowsAsync<OperationCanceledException>(() => result.BranchAsync<int>(
            (_, _) => throw new Exception(), (_, _) => throw new Exception(), (_, _) => throw new Exception(), cts.Token));
        await Assert.ThrowsAsync<ArgumentNullException>(() => result.EnsureAsync(null!));
        await Assert.ThrowsAsync<ArgumentException>(() => result.EnsureAsync((_, _) => Task.FromResult(true), default(RailError)));
        await Assert.ThrowsAsync<ArgumentNullException>(() => result.OnSuccessAsync(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => result.OnFailureAsync(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => result.MatchAsync<int>(null!, (_, _) => Task.FromResult(1)));
        await Assert.ThrowsAsync<ArgumentNullException>(() => result.MatchAsync<int>((_, _) => Task.FromResult(1), null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => result.BranchAsync<int>(
            (Func<int, CancellationToken, Task<bool>>)null!, (_, _) => throw new Exception(), (_, _) => throw new Exception()));
        await Assert.ThrowsAsync<ArgumentNullException>(() => result.BranchAsync<int>(
            (_, _) => Task.FromResult(false), null!, (_, _) => throw new Exception()));
        await Assert.ThrowsAsync<ArgumentNullException>(() => result.BranchAsync<int>(
            (_, _) => Task.FromResult(true), (_, _) => throw new Exception(), null!));
    }
}
