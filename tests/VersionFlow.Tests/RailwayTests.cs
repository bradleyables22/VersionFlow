using VersionFlow.Extensions.Railway;
using Xunit;

namespace VersionFlow.Tests;

public sealed class RailwayTests
{
    [Fact]
    public void Chain_transforms_saves_and_matches()
    {
        var observed = 0;
        var output = RailResult<string>.Passed("long name")
            .Ensure(s => s.Length <= 4, recover: s => s[..4])
            .Select(s => s.Length)
            .Then(n => RailResult<int>.Passed(n * 2))
            .OnSuccess(n => observed = n)
            .OnFailure(_ => Assert.Fail("Unexpected failure"))
            .Match(n => n.ToString(), _ => "failed");
        Assert.Equal("8", output);
        Assert.Equal(8, observed);
    }

    [Fact]
    public void Failure_skips_every_success_callback_and_recovery()
    {
        var result = RailResult<int>.Failed("original")
            .Ensure(_ => throw new Exception(), recover: _ => throw new Exception())
            .Select<string>(_ => throw new Exception())
            .Then<int>(_ => throw new Exception())
            .OnSuccess(_ => Assert.Fail("Must skip"));
        Assert.True(result.IsFailed);
        Assert.Equal("original", Assert.Single(result.Errors).Message);
        Assert.Throws<InvalidOperationException>(() => result.Value);
        Assert.Equal("original", result.Match(_ => "wrong", e => e[0].Message));
    }

    [Fact]
    public void Recovery_runs_once_and_rechecks_once()
    {
        var checks = 0;
        var repairs = 0;
        var code = new RailErrorCode("customer.name");
        var result = RailResult<string>.Passed("abc")
            .Ensure(s => { checks++; return s.Length <= 1; }, "too long",
                s => { repairs++; return s[..2]; }, code);
        Assert.Equal(2, checks);
        Assert.Equal(1, repairs);
        var error = Assert.Single(result.Errors);
        Assert.Equal(code, error.Code);
        Assert.Equal("too long", error.Message);
        Assert.Equal(RailErrorCodes.RecoveryFailed, error.RecoveryFailure!.Value.Code);
        Assert.Null(error.RecoveryFailure.Value.Exception);
    }

    [Fact]
    public void Recovery_exception_is_attached_to_original_error()
    {
        var exception = new InvalidOperationException("repair failed");
        var result = RailResult<int>.Passed(1).Ensure(n => n > 2, "too small", _ => throw exception);
        var error = Assert.Single(result.Errors);
        Assert.Equal("too small", error.Message);
        Assert.Same(exception, error.RecoveryFailure!.Value.Exception);
        Assert.Null(error.Exception);
    }

    [Fact]
    public void Passing_validation_does_not_recover()
    {
        var result = RailResult<int>.Passed(3);
        Assert.Same(result, result.Ensure(n => n > 2, recover: _ => throw new Exception()));
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Callback_exceptions_are_captured_but_cancellation_propagates()
    {
        var exception = new Exception("broken");
        var start = RailResult<int>.Passed(1);
        Assert.Same(exception, start.Select<int>(_ => throw exception).Errors[0].Exception);
        Assert.Same(exception, start.Then<int>(_ => throw exception).Errors[0].Exception);
        Assert.Same(exception, start.Ensure(_ => throw exception).Errors[0].Exception);
        Assert.Throws<OperationCanceledException>(() => start.Select<int>(_ => throw new OperationCanceledException()));
        Assert.Throws<OperationCanceledException>(() => start.Then<int>(_ => throw new OperationCanceledException()));
        Assert.Throws<OperationCanceledException>(() => start.Ensure(_ => throw new OperationCanceledException()));
        Assert.Throws<OperationCanceledException>(() => start.Ensure(_ => false, recover: _ => throw new OperationCanceledException()));
        Assert.True(start.Then<int>(_ => null!).IsFailed);
    }

    [Fact]
    public void Recheck_exceptions_are_recovery_failures()
    {
        var exception = new Exception("recheck");
        var result = RailResult<int>.Passed(0).Ensure(n => n == 0 ? false : throw exception, recover: _ => 1);
        Assert.Same(exception, result.Errors[0].RecoveryFailure!.Value.Exception);
    }

    [Fact]
    public void Errors_are_defensively_copied_and_invalid_failures_rejected()
    {
        var errors = new[] { new RailError("first"), new RailError("second") };
        var result = RailResult<int>.Failed(errors);
        errors[0] = new RailError("changed");
        Assert.Equal("first", result.Errors[0].Message);
        Assert.Throws<NotSupportedException>(() => ((IList<RailError>)result.Errors)[0] = new RailError("changed"));
        Assert.Throws<ArgumentException>(() => RailResult<int>.Failed(Array.Empty<RailError>()));
        Assert.Throws<ArgumentException>(() => RailResult<int>.Failed(default(RailError)));
        Assert.Throws<ArgumentException>(() => new RailErrorCode(" "));
        Assert.Throws<ArgumentNullException>(() => result.Select<int>(null!));
    }

    [Fact]
    public void Defaults_custom_codes_and_null_success_are_supported()
    {
        Assert.Equal(new RailErrorCode("unspecified"), default(RailErrorCode));
        Assert.Equal(RailErrorCodes.Unspecified, RailResult<int>.Failed("bad").Errors[0].Code);
        Assert.Equal(RailErrorCodes.ValidationFailed, RailResult<int>.Passed(0).Ensure(_ => false).Errors[0].Code);
        Assert.Equal(RailErrorCodes.Exception, RailResult<int>.Failed(new Exception()).Errors[0].Code);
        Assert.NotEqual(new RailErrorCode("custom"), new RailErrorCode("CUSTOM"));
        Assert.Null(RailResult<string?>.Passed(null).Value);
        Assert.Equal(RailUnit.Value, RailResult<RailUnit>.Passed(RailUnit.Value).Value);
    }

    [Fact]
    public void Observers_and_match_propagate_exceptions_without_changing_outcome()
    {
        var success = RailResult<int>.Passed(1);
        var failure = RailResult<int>.Failed("bad");
        Assert.Same(success, success.OnFailure(_ => throw new Exception()));
        Assert.Same(failure, failure.OnSuccess(_ => throw new Exception()));
        Assert.Throws<InvalidOperationException>(() => success.OnSuccess(_ => throw new InvalidOperationException()));
        Assert.Throws<InvalidOperationException>(() => failure.OnFailure(_ => throw new InvalidOperationException()));
        Assert.Throws<InvalidOperationException>(() => success.Match<int>(_ => throw new InvalidOperationException(), _ => 0));
    }

    [Fact]
    public async Task Async_operations_capture_failures_forward_tokens_and_skip_failed_results()
    {
        using var cts = new CancellationTokenSource();
        var result = await RailResult<int>.Passed(2).SelectAsync(async (n, ct) =>
        {
            Assert.Equal(cts.Token, ct);
            await Task.Yield();
            return n.ToString();
        }, cts.Token);
        Assert.Equal("2", result.Value);
        var failed = await result.ThenAsync<int>(async (_, _) =>
        {
            await Task.Yield();
            throw new InvalidOperationException("async");
        });
        Assert.IsType<InvalidOperationException>(failed.Errors[0].Exception);
        var skipped = await failed.SelectAsync<string>((_, _) => throw new Exception());
        Assert.Equal(failed.Errors, skipped.Errors);
        Assert.True((await result.ThenAsync<int>((_, _) => Task.FromResult<RailResult<int>>(null!))).IsFailed);
        Assert.True((await result.ThenAsync((_, _) => Task.FromResult(RailResult<int>.Passed(5)))).IsPassed);
        Assert.True((await result.SelectAsync<int>((_, _) => throw new Exception())).IsFailed);
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            result.ThenAsync<int>((_, _) => throw new OperationCanceledException()));
        cts.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            result.SelectAsync((_, _) => Task.FromResult(1), cts.Token));
    }
}
