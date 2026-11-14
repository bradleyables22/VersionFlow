using VersionFlow.Extensions.Railway;
using Xunit;

namespace VersionFlow.Tests;

public sealed class RailwayTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Branches_choose_one_path_and_return_its_result(bool condition)
    {
        var expected = RailResult<string>.Passed("selected");
        var checks = 0;
        var calls = 0;
        RailResult<string> Selected(int value)
        {
            Assert.Equal(42, value);
            calls++;
            return expected;
        }
        RailResult<string> Unselected(int _) => throw new Exception("Wrong path");
        var start = RailResult<int>.Passed(42);
        var result = start.Branch<string>(n => { checks++; return condition; },
            condition ? Selected : Unselected, condition ? Unselected : Selected);
        Assert.Same(expected, result);
        Assert.Equal("selected!", result.Select(s => s + "!").Value);

        using var cts = new CancellationTokenSource();
        async Task<RailResult<string>> SelectedAsync(int value, CancellationToken ct)
        {
            await Task.Yield();
            Assert.Equal(cts.Token, ct);
            return Selected(value);
        }
        Task<RailResult<string>> UnselectedAsync(int _, CancellationToken ct) => throw new Exception("Wrong path");
        Assert.Same(expected, await start.BranchAsync<string>(n => { checks++; return condition; },
            condition ? SelectedAsync : UnselectedAsync,
            condition ? UnselectedAsync : SelectedAsync, cts.Token));
        Assert.Equal(2, checks);
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task Branches_preserve_failures_and_skip_callbacks()
    {
        var failed = RailResult<int>.Failed(new[] { new RailError("first"), new RailError("second") });
        Assert.Equal(failed.Errors, failed.Branch<string>(_ => throw new Exception(),
            _ => throw new Exception(), _ => throw new Exception()).Errors);
        Assert.Equal(failed.Errors, (await failed.BranchAsync<string>(_ => throw new Exception(),
            (_, _) => throw new Exception(), (_, _) => throw new Exception())).Errors);
        var selected = RailResult<string>.Failed("branch failure");
        var branched = RailResult<int>.Passed(1).Branch(_ => true, _ => selected, _ => throw new Exception());
        Assert.Same(selected, branched);
        Assert.Equal(selected.Errors, branched.Then<string>(_ => throw new Exception()).Errors);
        Assert.Same(selected, await RailResult<int>.Passed(1).BranchAsync(_ => false,
            (_, _) => throw new Exception(), (_, _) => Task.FromResult(selected)));
    }

    [Fact]
    public async Task Branches_capture_exceptions_and_null_results_but_propagate_cancellation()
    {
        var start = RailResult<int>.Passed(1);
        var exception = new InvalidOperationException("broken");
        Assert.Same(exception, start.Branch<int>(_ => throw exception, n => RailResult<int>.Passed(n),
            n => RailResult<int>.Passed(n)).Errors[0].Exception);
        Assert.Same(exception, start.Branch<int>(_ => true, _ => throw exception,
            n => RailResult<int>.Passed(n)).Errors[0].Exception);
        Assert.True(start.Branch<int>(_ => false, n => RailResult<int>.Passed(n), _ => null!).IsFailed);
        Assert.Same(exception, (await start.BranchAsync<int>(_ => throw exception,
            (_, _) => throw new Exception(), (_, _) => throw new Exception())).Errors[0].Exception);
        Assert.Same(exception, (await start.BranchAsync<int>(_ => false,
            (_, _) => throw new Exception(), async (_, _) => { await Task.Yield(); throw exception; })).Errors[0].Exception);
        Assert.True((await start.BranchAsync<int>(_ => true,
            (_, _) => Task.FromResult<RailResult<int>>(null!), (_, _) => throw new Exception())).IsFailed);
        Assert.Throws<OperationCanceledException>(() => start.Branch<int>(_ => throw new OperationCanceledException(),
            _ => throw new Exception(), _ => throw new Exception()));
        Assert.Throws<OperationCanceledException>(() => start.Branch<int>(_ => true,
            _ => throw new OperationCanceledException(), _ => throw new Exception()));
        await Assert.ThrowsAsync<OperationCanceledException>(() => start.BranchAsync<int>(_ => true,
            (_, _) => throw new OperationCanceledException(), (_, _) => throw new Exception()));
        await Assert.ThrowsAsync<OperationCanceledException>(() => start.BranchAsync<int>(_ => throw new OperationCanceledException(),
            (_, _) => throw new Exception(), (_, _) => throw new Exception()));
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        foreach (var input in new[] { start, RailResult<int>.Failed("original") })
            await Assert.ThrowsAsync<OperationCanceledException>(() => input.BranchAsync<int>(_ => throw new Exception(),
                (_, _) => throw new Exception(), (_, _) => throw new Exception(), cts.Token));
    }

    [Fact]
    public async Task Branches_validate_all_arguments_even_on_failure()
    {
        var failed = RailResult<int>.Failed("original");
        Assert.Throws<ArgumentNullException>(() => failed.Branch<int>(null!, n => RailResult<int>.Passed(n), n => RailResult<int>.Passed(n)));
        Assert.Throws<ArgumentNullException>(() => failed.Branch<int>(_ => false, null!, n => RailResult<int>.Passed(n)));
        Assert.Throws<ArgumentNullException>(() => failed.Branch<int>(_ => true, n => RailResult<int>.Passed(n), null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => failed.BranchAsync<int>((Func<int, bool>)null!, (_, _) => throw new Exception(), (_, _) => throw new Exception()));
        await Assert.ThrowsAsync<ArgumentNullException>(() => failed.BranchAsync<int>(_ => false, null!, (_, _) => throw new Exception()));
        await Assert.ThrowsAsync<ArgumentNullException>(() => failed.BranchAsync<int>(_ => true, (_, _) => throw new Exception(), null!));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Explicit_validation_requires_the_named_boolean(bool condition)
    {
        var start = RailResult<int>.Passed(1);
        Assert.Equal(condition, start.EnsureTrue(_ => condition).IsPassed);
        Assert.Equal(!condition, start.EnsureFalse(_ => condition).IsPassed);
        Assert.Equal(condition, start.Ensure(_ => condition).IsPassed);
        var error = new RailError("invalid", new RailErrorCode("custom"));
        Assert.Equal(condition, start.Ensure(_ => condition, error).IsPassed);
        Assert.Equal(error, Assert.Single(start.EnsureFalse(_ => true, error).Errors));
        Assert.Same(start, start.EnsureFalse(_ => false, recover: _ => throw new Exception()));
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, false)]
    public void EnsureFalse_recovers_once_and_rechecks_for_false(int corrected, bool passes)
    {
        var checks = 0;
        var repairs = 0;
        var code = new RailErrorCode("positive");
        var result = RailResult<int>.Passed(2).EnsureFalse(
            n => { checks++; return n > 0; }, "must not be positive",
            _ => { repairs++; return corrected; }, code);
        Assert.Equal(2, checks);
        Assert.Equal(1, repairs);
        Assert.Equal(passes, result.IsPassed);
        if (passes) Assert.Equal(corrected, result.Value);
        else
        {
            var error = Assert.Single(result.Errors);
            Assert.Equal(code, error.Code);
            Assert.NotNull(error.RecoveryFailure);
        }
    }

    [Fact]
    public void EnsureFalse_preserves_skipping_exception_and_argument_behavior()
    {
        var failed = RailResult<int>.Failed("original");
        Assert.Same(failed, failed.EnsureFalse(_ => throw new Exception(), recover: _ => throw new Exception()));
        Assert.Throws<ArgumentNullException>(() => failed.EnsureFalse(null!));
        var start = RailResult<int>.Passed(1);
        var exception = new Exception("broken");
        Assert.Same(exception, start.EnsureFalse(_ => throw exception,
            recover: _ => throw new Exception()).Errors[0].Exception);
        Assert.Same(exception, start.EnsureFalse(_ => true,
            recover: _ => throw exception).Errors[0].RecoveryFailure!.Value.Exception);
        Assert.Same(exception, start.EnsureFalse(n => n == 1 ? true : throw exception,
            recover: _ => 0).Errors[0].RecoveryFailure!.Value.Exception);
        Assert.Throws<OperationCanceledException>(() => start.EnsureFalse(_ => throw new OperationCanceledException()));
        Assert.Throws<OperationCanceledException>(() => start.EnsureFalse(_ => true,
            recover: _ => throw new OperationCanceledException()));
    }

    [Fact]
    public void Chain_transforms_saves_and_matches()
    {
        var observed = 0;
        var output = RailResult<string>.Passed("long name")
            .EnsureTrue(s => s.Length <= 4, recover: s => s[..4])
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
            .EnsureTrue(_ => throw new Exception(), recover: _ => throw new Exception())
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
            .EnsureTrue(s => { checks++; return s.Length <= 1; }, "too long",
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
        var result = RailResult<int>.Passed(1).EnsureTrue(n => n > 2, "too small", _ => throw exception);
        var error = Assert.Single(result.Errors);
        Assert.Equal("too small", error.Message);
        Assert.Same(exception, error.RecoveryFailure!.Value.Exception);
        Assert.Null(error.Exception);
    }

    [Fact]
    public void Passing_validation_does_not_recover()
    {
        var result = RailResult<int>.Passed(3);
        Assert.Same(result, result.EnsureTrue(n => n > 2, recover: _ => throw new Exception()));
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Callback_exceptions_are_captured_but_cancellation_propagates()
    {
        var exception = new Exception("broken");
        var start = RailResult<int>.Passed(1);
        Assert.Same(exception, start.Select<int>(_ => throw exception).Errors[0].Exception);
        Assert.Same(exception, start.Then<int>(_ => throw exception).Errors[0].Exception);
        Assert.Same(exception, start.EnsureTrue(_ => throw exception).Errors[0].Exception);
        Assert.Throws<OperationCanceledException>(() => start.Select<int>(_ => throw new OperationCanceledException()));
        Assert.Throws<OperationCanceledException>(() => start.Then<int>(_ => throw new OperationCanceledException()));
        Assert.Throws<OperationCanceledException>(() => start.EnsureTrue(_ => throw new OperationCanceledException()));
        Assert.Throws<OperationCanceledException>(() => start.EnsureTrue(_ => false, recover: _ => throw new OperationCanceledException()));
        Assert.True(start.Then<int>(_ => null!).IsFailed);
    }

    [Fact]
    public void Recheck_exceptions_are_recovery_failures()
    {
        var exception = new Exception("recheck");
        var result = RailResult<int>.Passed(0).EnsureTrue(n => n == 0 ? false : throw exception, recover: _ => 1);
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
        Assert.Equal(RailErrorCodes.ValidationFailed, RailResult<int>.Passed(0).EnsureTrue(_ => false).Errors[0].Code);
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
