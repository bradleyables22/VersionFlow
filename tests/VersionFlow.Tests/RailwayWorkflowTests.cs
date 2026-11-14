using VersionFlow.Extensions.Railway;
using Xunit;

namespace VersionFlow.Tests;

public sealed class RailwayWorkflowTests
{
    [Fact]
    public async Task Try_wraps_values_and_exceptions_but_not_cancellation()
    {
        Assert.Equal(42, RailResult<int>.Try(() => 42).Value);
        Assert.Null(RailResult<string?>.Try(() => null).Value);
        var error = new InvalidOperationException("broken");
        Assert.Same(error, RailResult<int>.Try(() => throw error).Errors[0].Exception);
        using var cts = new CancellationTokenSource();
        Assert.Equal(42, (await RailResult<int>.TryAsync(async ct =>
        {
            await Task.Yield();
            Assert.Equal(cts.Token, ct);
            return 42;
        }, cts.Token)).Value);
        Assert.Same(error, (await RailResult<int>.TryAsync(_ => Task.FromException<int>(error))).Errors[0].Exception);
        Assert.Throws<OperationCanceledException>(() => RailResult<int>.Try(() => throw new OperationCanceledException()));
        await Assert.ThrowsAsync<OperationCanceledException>(() => RailResult<int>.TryAsync(_ => throw new OperationCanceledException()));
        Assert.Throws<ArgumentNullException>(() => RailResult<int>.Try(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => RailResult<int>.TryAsync(null!));
        cts.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => RailResult<int>.TryAsync(_ => throw new Exception(), cts.Token));
    }

    [Fact]
    public async Task Recovery_replaces_failure_and_preserves_diagnostics_if_it_throws()
    {
        var passed = RailResult<int>.Passed(42);
        var failed = RailResult<int>.Failed("original");
        Assert.Same(passed, passed.Recover(_ => throw new Exception()));
        Assert.Same(passed, await passed.RecoverAsync((_, _) => throw new Exception()));
        Assert.Same(passed, failed.Recover(errors =>
        {
            Assert.Same(failed.Errors, errors);
            return passed;
        }));
        using var cts = new CancellationTokenSource();
        Assert.Same(passed, await failed.RecoverAsync(async (errors, ct) =>
        {
            await Task.Yield();
            Assert.Equal(cts.Token, ct);
            Assert.Same(failed.Errors, errors);
            return passed;
        }, cts.Token));
        var fallback = RailResult<int>.Failed("fallback");
        Assert.Same(fallback, failed.Recover(_ => fallback));
        var exception = new Exception("recovery");
        foreach (var result in new[] { failed.Recover(_ => throw exception),
            await failed.RecoverAsync((_, _) => Task.FromException<RailResult<int>>(exception)) })
        {
            Assert.Equal("original", result.Errors[0].Message);
            Assert.Same(exception, result.Errors[1].Exception);
        }
        Assert.Equal(2, failed.Recover(_ => null!).Errors.Count);
        Assert.Equal(2, (await failed.RecoverAsync((_, _) => Task.FromResult<RailResult<int>>(null!))).Errors.Count);
        Assert.Throws<OperationCanceledException>(() => failed.Recover(_ => throw new OperationCanceledException()));
        await Assert.ThrowsAsync<OperationCanceledException>(() => failed.RecoverAsync((_, _) => throw new OperationCanceledException()));
        Assert.Throws<ArgumentNullException>(() => passed.Recover(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => passed.RecoverAsync(null!));
    }

    [Fact]
    public async Task Error_mapping_preserves_order_and_can_keep_diagnostic_details()
    {
        var exception = new Exception("diagnostic");
        var detail = new RailRecoveryFailure("correction");
        var failed = RailResult<int>.Failed(new[] { new RailError("one", exception: exception, recoveryFailure: detail), new RailError("two") });
        RailError Map(RailError e) => new("app: " + e.Message, new RailErrorCode("app"), e.Exception, e.RecoveryFailure);
        var sync = failed.MapErrors(Map);
        var asyncResult = await failed.MapErrorsAsync(async (e, ct) => { await Task.Yield(); return Map(e); });
        Assert.Equal(sync.Errors, asyncResult.Errors);
        Assert.Equal(new[] { "app: one", "app: two" }, sync.Errors.Select(e => e.Message));
        Assert.Same(exception, sync.Errors[0].Exception);
        Assert.Equal(detail, sync.Errors[0].RecoveryFailure);
        var passed = RailResult<int>.Passed(1);
        Assert.Same(passed, passed.MapErrors(_ => throw new Exception()));
        Assert.Same(passed, await passed.MapErrorsAsync((_, _) => throw new Exception()));
        foreach (var result in new[] { failed.MapErrors(_ => default),
            await failed.MapErrorsAsync((_, _) => Task.FromResult(default(RailError))) })
        {
            Assert.Equal(failed.Errors, result.Errors.Take(2));
            Assert.IsType<ArgumentException>(result.Errors[2].Exception);
        }
        Assert.Throws<OperationCanceledException>(() => failed.MapErrors(_ => throw new OperationCanceledException()));
        await Assert.ThrowsAsync<OperationCanceledException>(() => failed.MapErrorsAsync((_, _) => throw new OperationCanceledException()));
        Assert.Throws<ArgumentNullException>(() => passed.MapErrors(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => passed.MapErrorsAsync(null!));
    }

    [Theory]
    [InlineData(RailFailureMode.StopOnFirstFailure, 2, 1)]
    [InlineData(RailFailureMode.CollectAll, 4, 2)]
    public async Task Traversal_controls_work_and_accumulates_errors_in_order(RailFailureMode mode, int calls, int errors)
    {
        var visited = new List<int>();
        RailResult<string> Process(int n)
        {
            visited.Add(n);
            return n % 2 == 0 ? RailResult<string>.Failed(n.ToString()) : RailResult<string>.Passed(n.ToString());
        }
        var result = RailResult.Traverse(new[] { 1, 2, 3, 4 }, Process, mode);
        Assert.Equal(calls, visited.Count);
        Assert.Equal(errors, result.Errors.Count);
        visited.Clear();
        using var cts = new CancellationTokenSource();
        var asyncResult = await RailResult.TraverseAsync(new[] { 1, 2, 3, 4 }, async (n, ct) =>
        {
            await Task.Yield();
            Assert.Equal(cts.Token, ct);
            return Process(n);
        }, mode, cts.Token);
        Assert.Equal(Enumerable.Range(1, calls), visited);
        Assert.Equal(result.Errors, asyncResult.Errors);
    }

    [Fact]
    public async Task Collections_handle_empty_success_null_results_and_enumeration_failures()
    {
        Assert.Empty(RailResult.Combine(Array.Empty<RailResult<int>>()).Value);
        var results = new[] { RailResult<int>.Passed(1), RailResult<int>.Passed(2) };
        var combined = RailResult.Combine(results);
        Assert.Equal(new[] { 1, 2 }, combined.Value);
        Assert.Throws<NotSupportedException>(() => ((IList<int>)combined.Value).Add(3));
        Assert.Equal(combined.Value, (await RailResult.CombineAsync(results.Select(Task.FromResult))).Value);
        var errors = new[] { RailResult<int>.Failed("one"), RailResult<int>.Failed("two") };
        Assert.Equal(new[] { "one", "two" }, RailResult.Combine(errors).Errors.Select(e => e.Message));
        Assert.Single(RailResult.Combine(errors, RailFailureMode.StopOnFirstFailure).Errors);
        Assert.True(RailResult.Combine(new RailResult<int>[] { null! }).IsFailed);
        Assert.True((await RailResult.CombineAsync(new Task<RailResult<int>>[] { null! })).IsFailed);
        var exception = new Exception("enumeration");
        IEnumerable<int> Broken()
        {
            yield return 1;
            throw exception;
        }
        var enumerated = RailResult.Traverse(Broken(), _ => RailResult<int>.Failed("item"), RailFailureMode.CollectAll);
        Assert.Equal("item", enumerated.Errors[0].Message);
        Assert.Same(exception, enumerated.Errors[1].Exception);
        Assert.Same(exception, (await RailResult.TraverseAsync(Broken(), (n, _) => Task.FromResult(RailResult<int>.Passed(n)))).Errors[0].Exception);
        Assert.Throws<ArgumentOutOfRangeException>(() => RailResult.Combine(results, (RailFailureMode)99));
        Assert.Throws<ArgumentNullException>(() => RailResult.Traverse<int, int>(null!, RailResult<int>.Passed));
        Assert.Throws<OperationCanceledException>(() => RailResult.Traverse<int, int>(new[] { 1 }, _ => throw new OperationCanceledException()));
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => RailResult.TraverseAsync<int, int>(Array.Empty<int>(),
            (_, _) => throw new Exception(), cancellationToken: cts.Token));
    }

    [Fact]
    public async Task Task_extensions_compose_mixed_steps_and_recover_failed_pipelines()
    {
        var observations = 0;
        var output = await RailResult<int>.TryAsync(_ => Task.FromResult(1))
            .Select(n => n + 1)
            .EnsureTrue(n => n == 2)
            .ThenAsync((n, _) => Task.FromResult(RailResult<int>.Failed("service")))
            .MapErrors(e => new RailError("mapped: " + e.Message))
            .OnFailureAsync((errors, _) => { Assert.Equal("mapped: service", errors[0].Message); observations++; return Task.CompletedTask; })
            .RecoverAsync((_, _) => Task.FromResult(RailResult<int>.Passed(3)))
            .BranchAsync((n, _) => Task.FromResult(n == 3),
                (n, _) => Task.FromResult(RailResult<int>.Passed(n * 2)),
                (_, _) => throw new Exception())
            .EnsureFalseAsync((n, _) => Task.FromResult(n == 0))
            .OnSuccess(n => { Assert.Equal(6, n); observations++; })
            .Match(n => n.ToString(), _ => "failed");
        Assert.Equal("6", output);
        Assert.Equal(2, observations);
    }

    [Fact]
    public async Task Task_extensions_propagate_upstream_faults_and_cancel_waiting()
    {
        var exception = new Exception("upstream");
        var faulted = Task.FromException<RailResult<int>>(exception);
        Assert.Same(exception, await Assert.ThrowsAsync<Exception>(() => faulted.Recover(_ => RailResult<int>.Passed(1))));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Task.FromResult<RailResult<int>>(null!).Select(n => n));
        var pending = new TaskCompletionSource<RailResult<int>>();
        using var cts = new CancellationTokenSource();
        var chained = pending.Task.ThenAsync<int, int>((n, _) => throw new Exception(), cts.Token);
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => chained);
        Assert.False(pending.Task.IsCompleted);
        pending.SetResult(RailResult<int>.Passed(1));
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            ((Task<RailResult<int>>)null!).OnSuccess(_ => { }));
        await Assert.ThrowsAsync<ArgumentNullException>(() => pending.Task.Select<int, int>(null!));
        foreach (var result in new[] { RailResult<int>.Passed(1), RailResult<int>.Failed("bad") })
        {
            await Assert.ThrowsAsync<OperationCanceledException>(() => result.RecoverAsync((_, _) => throw new Exception(), cts.Token));
            await Assert.ThrowsAsync<OperationCanceledException>(() => result.MapErrorsAsync((_, _) => throw new Exception(), cts.Token));
        }
    }
}
