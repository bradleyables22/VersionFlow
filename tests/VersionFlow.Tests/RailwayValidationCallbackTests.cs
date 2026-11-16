using VersionFlow.Extensions.Railway;
using Xunit;

namespace VersionFlow.Tests;

public sealed class RailwayValidationCallbackTests
{
    public static IEnumerable<object[]> Overloads()
    {
        for (var overload = 0; overload < 6; overload++)
            foreach (var taskSource in new[] { false, true })
                yield return [overload, taskSource];
    }

    [Theory]
    [MemberData(nameof(Overloads))]
    public async Task Sync_checks_report_only_their_final_outcome(int overload, bool taskSource)
    {
        var observations = new List<(int Value, bool Recovered)>();
        var failures = new List<RailError>();
        var error = new RailError("must be positive", new RailErrorCode("positive"));
        Func<int, bool> predicate = n => overload / 2 == 2 ? n <= 0 : n > 0;
        async Task<RailResult<int>> Check(RailResult<int> input, Func<int, int>? recover = null) =>
            await CheckSync(input, overload, taskSource, predicate, error, recover,
                (value, recovered) => observations.Add((value, recovered)), failures.Add);

        var original = 2.ToRailResult();
        Assert.Same(original, await Check(original));
        Assert.Equal([(2, false)], observations);
        Assert.Empty(failures);

        var corrected = await Check((-1).ToRailResult(), _ => 1);
        Assert.Equal(1, corrected.Value);
        Assert.Equal([(2, false), (1, true)], observations);
        Assert.Empty(failures);

        var failed = await Check((-1).ToRailResult());
        Assert.Equal(error, Assert.Single(failed.Errors));
        Assert.Equal(error, Assert.Single(failures));
        var failedRecovery = await Check((-1).ToRailResult(), _ => 0);
        Assert.NotNull(Assert.Single(failedRecovery.Errors).RecoveryFailure);
        Assert.Equal(failedRecovery.Errors[0], failures[1]);

        Assert.Same(failed, await Check(failed, _ => throw new Exception("Must skip")));
        Assert.Equal(2, observations.Count);
        Assert.Equal(2, failures.Count);
    }

    [Theory]
    [MemberData(nameof(Overloads))]
    public async Task Async_checks_support_both_observer_styles_and_recovery(int overload, bool taskSource)
    {
        using var cts = new CancellationTokenSource();
        var token = cts.Token;
        var error = new RailError("must be positive", new RailErrorCode("positive"));
        foreach (var asynchronousObservers in new[] { false, true })
        {
            var passed = new List<(int, bool)>();
            var failed = new List<RailError>();
            async Task<bool> Predicate(int n, CancellationToken ct)
            {
                await Task.Yield();
                Assert.Equal(token, ct);
                return overload / 2 == 2 ? n <= 0 : n > 0;
            }
            async Task OnPassed(int value, bool recovered, CancellationToken ct)
            {
                await Task.Yield();
                Assert.Equal(token, ct);
                passed.Add((value, recovered));
            }
            async Task OnFailed(RailError failure, CancellationToken ct)
            {
                await Task.Yield();
                Assert.Equal(token, ct);
                failed.Add(failure);
            }
            Task<RailResult<int>> Check(RailResult<int> input, Func<int, CancellationToken, Task<int>>? recover = null) =>
                CheckAsync(input, overload, taskSource, Predicate, error, recover, token,
                    asynchronousObservers ? null : (value, recovered) => passed.Add((value, recovered)),
                    asynchronousObservers ? null : failed.Add,
                    asynchronousObservers ? OnPassed : null,
                    asynchronousObservers ? OnFailed : null);

            var original = 2.ToRailResult();
            Assert.Same(original, await Check(original));
            Assert.Equal([(2, false)], passed);
            Assert.Empty(failed);
            var corrected = await Check((-1).ToRailResult(), (n, ct) =>
            {
                Assert.Equal(token, ct);
                return Task.FromResult(1);
            });
            Assert.Equal(1, corrected.Value);
            Assert.Equal([(2, false), (1, true)], passed);
            var failure = await Check((-1).ToRailResult());
            Assert.Equal(error, Assert.Single(failed));
            var failedRecovery = await Check((-1).ToRailResult(), (_, _) => Task.FromResult(0));
            Assert.NotNull(failedRecovery.Errors[0].RecoveryFailure);
            Assert.Equal(failedRecovery.Errors[0], failed[1]);
            Assert.Same(failure, await Check(failure, (_, _) => throw new Exception("Must skip")));
            Assert.Equal(2, passed.Count);
            Assert.Equal(2, failed.Count);
        }
    }

    [Fact]
    public void Later_rules_do_not_report_earlier_failures_or_recovery()
    {
        var logs = new List<string>();
        var failed = "".ToRailResult()
            .EnsureFalse(string.IsNullOrEmpty, "Title required", onFailed: e => logs.Add(e.Message))
            .EnsureFalse(_ => throw new Exception("Skipped predicate"), "Content required",
                onPassed: (_, _) => throw new Exception("Skipped observer"), onFailed: e => logs.Add(e.Message));
        Assert.Equal("Title required", Assert.Single(logs));
        Assert.True(failed.IsFailed);

        var recoveredFlags = new List<bool>();
        var passed = "long".ToRailResult()
            .EnsureTrue(s => s.Length == 1, recover: s => s[..1], onPassed: (_, r) => recoveredFlags.Add(r))
            .EnsureTrue(s => s.Length == 1, onPassed: (_, r) => recoveredFlags.Add(r));
        Assert.Equal("l", passed.Value);
        Assert.Equal([true, false], recoveredFlags);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task Failure_observers_receive_predicate_and_recovery_exceptions(int stage)
    {
        var exception = new InvalidOperationException("broken");
        var checks = 0;
        bool Predicate(int n)
        {
            checks++;
            if (stage == 0 || stage == 2 && checks == 2) throw exception;
            return false;
        }
        int Recover(int n) => stage == 1 ? throw exception : n;
        RailError? observed = null;
        var result = 1.ToRailResult().EnsureTrue(Predicate, recover: Recover, onFailed: e => observed = e);
        Assert.Equal(Assert.Single(result.Errors), observed);
        Assert.Same(exception, stage == 0 ? observed!.Value.Exception : observed!.Value.RecoveryFailure!.Value.Exception);

        checks = 0;
        observed = null;
        result = await 1.ToRailResult().EnsureTrueAsync(
            (n, _) => Task.FromResult(Predicate(n)), recover: (n, _) => Task.FromResult(Recover(n)),
            onFailedAsync: (e, _) => { observed = e; return Task.CompletedTask; });
        Assert.Equal(Assert.Single(result.Errors), observed);
        Assert.Same(exception, stage == 0 ? observed!.Value.Exception : observed!.Value.RecoveryFailure!.Value.Exception);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Async_observers_are_awaited_before_the_chain_continues(bool passes)
    {
        using var cts = new CancellationTokenSource();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var continued = false;
        async Task Observe(CancellationToken ct)
        {
            Assert.Equal(cts.Token, ct);
            entered.SetResult();
            await release.Task.WaitAsync(ct);
        }
        var pending = 1.ToRailResult().EnsureTrueAsync((_, _) => Task.FromResult(passes),
            cancellationToken: cts.Token,
            onPassedAsync: (_, recovered, ct) => { Assert.False(recovered); return Observe(ct); },
            onFailedAsync: (_, ct) => Observe(ct))
            .Match(_ => { continued = true; return true; }, _ => { continued = true; return false; });
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(pending.IsCompleted);
        Assert.False(continued);
        release.SetResult();
        Assert.Equal(passes, await pending.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.True(continued);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Observer_exceptions_propagate_without_becoming_validation_failures(bool passes)
    {
        var exception = new InvalidOperationException("observer failed");
        Assert.Same(exception, Assert.Throws<InvalidOperationException>(() => 1.ToRailResult().EnsureTrue(_ => passes,
            onPassed: (_, _) => throw exception, onFailed: _ => throw exception)));
        Assert.Same(exception, await Assert.ThrowsAsync<InvalidOperationException>(() => 1.ToRailResult().EnsureTrueAsync(
            (_, _) => Task.FromResult(passes), onPassed: (_, _) => throw exception, onFailed: _ => throw exception)));
        Assert.Same(exception, await Assert.ThrowsAsync<InvalidOperationException>(() => 1.ToRailResult().EnsureTrueAsync(
            (_, _) => Task.FromResult(passes),
            onPassedAsync: async (_, _, _) => { await Task.Yield(); throw exception; },
            onFailedAsync: async (_, _) => { await Task.Yield(); throw exception; })));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Duplicate_observers_are_rejected_even_when_the_rule_would_be_skipped(bool passedObservers)
    {
        var failed = RailResult<int>.Failed("earlier");
        var pending = new TaskCompletionSource<RailResult<int>>(TaskCreationOptions.RunContinuationsAsynchronously);
        Action<int, bool>? onPassed = passedObservers ? (_, _) => { } : null;
        Action<RailError>? onFailed = passedObservers ? null : _ => { };
        Func<int, bool, CancellationToken, Task>? onPassedAsync = passedObservers ? (_, _, _) => Task.CompletedTask : null;
        Func<RailError, CancellationToken, Task>? onFailedAsync = passedObservers ? null : (_, _) => Task.CompletedTask;
        var direct = failed.EnsureTrueAsync((_, _) => throw new Exception("Must not evaluate"),
            onPassed: onPassed, onFailed: onFailed, onPassedAsync: onPassedAsync, onFailedAsync: onFailedAsync);
        var chained = pending.Task.EnsureTrueAsync((_, _) => throw new Exception("Must not wait for source"),
            onPassed: onPassed, onFailed: onFailed, onPassedAsync: onPassedAsync, onFailedAsync: onFailedAsync);
        foreach (var attempt in new[] { direct, chained })
        {
            var error = await Assert.ThrowsAsync<ArgumentException>(() => attempt.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.Equal(passedObservers ? "onPassedAsync" : "onFailedAsync", error.ParamName);
        }
    }

    [Fact]
    public async Task Sync_and_async_observers_can_be_mixed_across_outcomes()
    {
        var passes = 0;
        var failures = 0;
        foreach (var pass in new[] { true, false })
        {
            await 1.ToRailResult().EnsureTrueAsync((_, _) => Task.FromResult(pass),
                onPassed: (_, _) => passes++, onFailedAsync: async (_, _) => { await Task.Yield(); failures++; });
            await 1.ToRailResult().EnsureFalseAsync((_, _) => Task.FromResult(!pass),
                onPassedAsync: async (_, _, _) => { await Task.Yield(); passes++; }, onFailed: _ => failures++);
        }
        Assert.Equal(2, passes);
        Assert.Equal(2, failures);
    }

    [Fact]
    public async Task Cancellation_skips_notifications_and_propagates_from_observers()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        foreach (var input in new[] { 1.ToRailResult(), RailResult<int>.Failed("earlier") })
            await Assert.ThrowsAsync<OperationCanceledException>(() => input.EnsureTrueAsync(
                (_, _) => throw new Exception("Skipped predicate"), cancellationToken: cts.Token,
                onPassed: (_, _) => throw new Exception("Skipped observer"), onFailed: _ => throw new Exception("Skipped observer")));
        await Assert.ThrowsAsync<OperationCanceledException>(() => 1.ToRailResult().EnsureTrueAsync(
            (_, _) => throw new OperationCanceledException(), onFailed: _ => throw new Exception("Not validation failure")));
        await Assert.ThrowsAsync<OperationCanceledException>(() => 1.ToRailResult().EnsureTrueAsync(
            (_, _) => Task.FromResult(false), recover: (_, _) => throw new OperationCanceledException(),
            onFailed: _ => throw new Exception("Not validation failure")));
        await Assert.ThrowsAsync<OperationCanceledException>(() => 1.ToRailResult().EnsureTrueAsync(
            (_, _) => Task.FromResult(true), onPassedAsync: (_, _, _) => throw new OperationCanceledException(),
            onFailed: _ => throw new Exception("Not validation failure")));
        Assert.Throws<OperationCanceledException>(() => 1.ToRailResult().EnsureTrue(_ => true,
            onPassed: (_, _) => throw new OperationCanceledException(), onFailed: _ => throw new Exception("Not validation failure")));
    }

    private static Task<RailResult<int>> CheckSync(RailResult<int> input, int overload, bool taskSource,
        Func<int, bool> predicate, RailError error,
        Func<int, int>? recover,
        Action<int, bool>? onPassed, Action<RailError>? onFailed)
    {
        if (taskSource)
        {
            var source = Task.FromResult(input);
            return overload switch
            {
                0 => source.Ensure(predicate, error.Message, recover, error.Code, onPassed, onFailed),
                1 => source.Ensure(predicate, error, recover, onPassed, onFailed),
                2 => source.EnsureTrue(predicate, error.Message, recover, error.Code, onPassed, onFailed),
                3 => source.EnsureTrue(predicate, error, recover, onPassed, onFailed),
                4 => source.EnsureFalse(predicate, error.Message, recover, error.Code, onPassed, onFailed),
                5 => source.EnsureFalse(predicate, error, recover, onPassed, onFailed),
                _ => throw new ArgumentOutOfRangeException(nameof(overload))
            };
        }
        return Task.FromResult(overload switch
        {
            0 => input.Ensure(predicate, error.Message, recover, error.Code, onPassed, onFailed),
            1 => input.Ensure(predicate, error, recover, onPassed, onFailed),
            2 => input.EnsureTrue(predicate, error.Message, recover, error.Code, onPassed, onFailed),
            3 => input.EnsureTrue(predicate, error, recover, onPassed, onFailed),
            4 => input.EnsureFalse(predicate, error.Message, recover, error.Code, onPassed, onFailed),
            5 => input.EnsureFalse(predicate, error, recover, onPassed, onFailed),
            _ => throw new ArgumentOutOfRangeException(nameof(overload))
        });
    }

    private static Task<RailResult<int>> CheckAsync(RailResult<int> input, int overload, bool taskSource,
        Func<int, CancellationToken, Task<bool>> predicate, RailError error,
        Func<int, CancellationToken, Task<int>>? recover, CancellationToken token,
        Action<int, bool>? onPassed, Action<RailError>? onFailed,
        Func<int, bool, CancellationToken, Task>? onPassedAsync, Func<RailError, CancellationToken, Task>? onFailedAsync)
    {
        if (taskSource)
        {
            var source = Task.FromResult(input);
            return overload switch
            {
                0 => source.EnsureAsync(predicate, error.Message, recover, error.Code, token, onPassed, onFailed, onPassedAsync, onFailedAsync),
                1 => source.EnsureAsync(predicate, error, recover, token, onPassed, onFailed, onPassedAsync, onFailedAsync),
                2 => source.EnsureTrueAsync(predicate, error.Message, recover, error.Code, token, onPassed, onFailed, onPassedAsync, onFailedAsync),
                3 => source.EnsureTrueAsync(predicate, error, recover, token, onPassed, onFailed, onPassedAsync, onFailedAsync),
                4 => source.EnsureFalseAsync(predicate, error.Message, recover, error.Code, token, onPassed, onFailed, onPassedAsync, onFailedAsync),
                5 => source.EnsureFalseAsync(predicate, error, recover, token, onPassed, onFailed, onPassedAsync, onFailedAsync),
                _ => throw new ArgumentOutOfRangeException(nameof(overload))
            };
        }
        return overload switch
        {
            0 => input.EnsureAsync(predicate, error.Message, recover, error.Code, token, onPassed, onFailed, onPassedAsync, onFailedAsync),
            1 => input.EnsureAsync(predicate, error, recover, token, onPassed, onFailed, onPassedAsync, onFailedAsync),
            2 => input.EnsureTrueAsync(predicate, error.Message, recover, error.Code, token, onPassed, onFailed, onPassedAsync, onFailedAsync),
            3 => input.EnsureTrueAsync(predicate, error, recover, token, onPassed, onFailed, onPassedAsync, onFailedAsync),
            4 => input.EnsureFalseAsync(predicate, error.Message, recover, error.Code, token, onPassed, onFailed, onPassedAsync, onFailedAsync),
            5 => input.EnsureFalseAsync(predicate, error, recover, token, onPassed, onFailed, onPassedAsync, onFailedAsync),
            _ => throw new ArgumentOutOfRangeException(nameof(overload))
        };
    }
}
