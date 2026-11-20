using VersionFlow.Extensions.Railway;
using Xunit;

namespace VersionFlow.Tests;

public sealed class RailwayCompletionTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Completion_observes_either_outcome_and_preserves_identity(bool passed)
    {
        var result = passed ? RailResult<int>.Passed(1) : RailResult<int>.Failed("bad");
        var calls = 0;
        Assert.Same(result, result.OnCompleted(r => { Assert.Same(result, r); calls++; }));
        Assert.Same(result, await Task.FromResult(result).OnCompleted(r => { Assert.Same(result, r); calls++; }));
        using var cts = new CancellationTokenSource();
        async Task Observe(RailResult<int> r, CancellationToken ct)
        {
            await Task.Yield();
            Assert.Same(result, r);
            Assert.Equal(cts.Token, ct);
            calls++;
        }
        Assert.Same(result, await result.OnCompletedAsync(Observe, cts.Token));
        Assert.Same(result, await Task.FromResult(result).OnCompletedAsync(Observe, cts.Token));
        Assert.Equal(4, calls);
        Assert.Throws<InvalidOperationException>(() => result.OnCompleted(_ => throw new InvalidOperationException()));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            result.OnCompletedAsync((_, _) => Task.FromException(new InvalidOperationException())));
    }

    [Fact]
    public async Task Completion_does_not_observe_faulted_canceled_or_null_sources()
    {
        var calls = 0;
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Task.FromException<RailResult<int>>(new InvalidOperationException()).OnCompleted(_ => calls++));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Task.FromResult<RailResult<int>>(null!).OnCompleted(_ => calls++));
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            ((Task<RailResult<int>>)null!).OnCompleted(_ => calls++));
        Assert.Throws<ArgumentNullException>(() => RailResult<int>.Passed(1).OnCompleted(null!));
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var pending = new TaskCompletionSource<RailResult<int>>();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            pending.Task.OnCompletedAsync((_, _) => { calls++; return Task.CompletedTask; }, cts.Token));
        Assert.False(pending.Task.IsCompleted);
        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task Batch_retains_inputs_outputs_duplicates_and_multiple_errors()
    {
        var inputs = new[] { "ok", "bad", "ok" };
        RailResult<int> Process(string item) => item == "bad"
            ? RailResult<int>.Failed(new[] { new RailError("one"), new RailError("two") })
            : RailResult<int>.Passed(item.Length);
        var sync = RailResult.TraverseResults(inputs, Process);
        var asyncBatch = await RailResult.TraverseResultsAsync(inputs, (item, _) => Task.FromResult(Process(item)));
        foreach (var batch in new[] { sync, asyncBatch })
        {
            Assert.Equal(inputs, batch.Entries.Select(e => e.Input));
            Assert.Equal(new[] { 2, 2 }, batch.SuccessfulValues);
            Assert.Equal(3, batch.Results.Count);
            Assert.Equal("bad", Assert.Single(batch.Failures).Input);
            Assert.Equal(2, batch.Errors.Count);
            Assert.Empty(batch.EnumerationErrors);
            Assert.True(batch.IsFailed);
            Assert.Same(batch.Entries[1].Result, batch.Results[1]);
            Assert.Throws<NotSupportedException>(() =>
                ((IList<RailResult<int>>)batch.Results).Clear());
        }
    }

    [Fact]
    public async Task Batch_converts_operation_exceptions_and_null_results_and_keeps_processing()
    {
        RailResult<int> Process(int n) => n switch
        {
            1 => throw new InvalidOperationException("broken"),
            2 => null!,
            _ => RailResult<int>.Passed(n)
        };
        var sync = RailResult.TraverseResults(new[] { 1, 2, 3 }, Process);
        var asyncBatch = await RailResult.TraverseResultsAsync(new[] { 1, 2, 3 },
            (n, _) => Task.FromResult(Process(n)));
        foreach (var batch in new[] { sync, asyncBatch })
        {
            Assert.Equal(2, batch.Failures.Count);
            Assert.Equal(new[] { 3 }, batch.SuccessfulValues);
            Assert.All(batch.Errors, e => Assert.IsType<InvalidOperationException>(e.Exception));
        }
        Assert.Throws<OperationCanceledException>(() =>
            RailResult.TraverseResults<int, int>(new[] { 1 }, _ => throw new OperationCanceledException()));
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            RailResult.TraverseResultsAsync<int, int>(new[] { 1 }, (_, _) => throw new OperationCanceledException()));
    }

    [Fact]
    public async Task Enumeration_errors_preserve_prior_results_without_inventing_an_input()
    {
        IEnumerable<string> Items()
        {
            yield return "first";
            throw new InvalidOperationException("enumeration");
        }
        var sync = RailResult.TraverseResults(Items(), RailResult<string>.Passed);
        var asyncBatch = await RailResult.TraverseResultsAsync(Items(),
            (n, _) => Task.FromResult(RailResult<string>.Passed(n)));
        foreach (var batch in new[] { sync, asyncBatch })
        {
            Assert.Equal("first", Assert.Single(batch.SuccessfulValues));
            Assert.Empty(batch.Failures);
            Assert.Single(batch.EnumerationErrors);
            Assert.Single(batch.Errors);
            Assert.True(batch.IsFailed);
        }
    }

    [Fact]
    public async Task Completion_waits_for_all_operations_and_bulk_cleanup_before_returning()
    {
        var deletion = new TaskCompletionSource<RailResult<int>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var cleanupStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cleanupFinished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var processed = new List<int>();
        var notified = false;
        using var cts = new CancellationTokenSource();
        var workflow = RailResult.TraverseResultsAsync(new[] { 1, 2 }, async (n, ct) =>
        {
            Assert.Equal(cts.Token, ct);
            processed.Add(n);
            return n == 1 ? await deletion.Task : RailResult<int>.Failed("bad");
        }, cts.Token)
        .OnFailure(batch => { Assert.Single(batch.Failures); notified = true; })
        .OnCompletedAsync(async (batch, ct) =>
        {
            Assert.True(notified);
            Assert.Equal(new[] { 1, 2 }, processed);
            Assert.Equal(cts.Token, ct);
            Assert.Equal(new[] { 10 }, batch.SuccessfulValues);
            cleanupStarted.SetResult();
            await cleanupFinished.Task;
        }, cts.Token);

        Assert.False(cleanupStarted.Task.IsCompleted);
        Assert.Equal(new[] { 1 }, processed);
        deletion.SetResult(RailResult<int>.Passed(10));
        await cleanupStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(workflow.IsCompleted);
        cleanupFinished.SetResult();
        var result = await workflow;
        Assert.True(result.IsFailed);
        Assert.Same(result, await Task.FromResult(result).OnCompleted(_ => { }));
        Assert.Same(result, await Task.FromResult(result).OnFailureAsync((_, _) => Task.CompletedTask));
        Assert.Same(result, result.OnCompleted(_ => { }));
        Assert.Throws<InvalidOperationException>(() => result.OnCompleted(_ => throw new InvalidOperationException()));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            result.OnCompletedAsync((_, _) => Task.FromException(new InvalidOperationException())));
    }

    [Fact]
    public async Task Empty_success_and_invalid_arguments_follow_existing_conventions()
    {
        var empty = RailResult.TraverseResults(Array.Empty<int>(), RailResult<int>.Passed);
        Assert.True(empty.IsPassed);
        Assert.Empty(empty.Entries);
        var calls = 0;
        empty.OnFailure(_ => calls++).OnCompleted(_ => calls++);
        Assert.Equal(1, calls);
        Assert.Throws<ArgumentNullException>(() => empty.OnFailure(null!));
        Assert.Throws<ArgumentNullException>(() => RailResult.TraverseResults<int, int>(null!, RailResult<int>.Passed));
        Assert.Throws<ArgumentNullException>(() => RailResult.TraverseResults<int, int>(new[] { 1 }, null!));
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            RailResult.TraverseResultsAsync(Array.Empty<int>(), (n, _) => Task.FromResult(RailResult<int>.Passed(n)), cts.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            empty.OnCompletedAsync((_, _) => Task.CompletedTask, cts.Token));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Task.FromResult<RailBatchResult<int, int>>(null!).OnCompleted(_ => { }));
    }
}
