# VersionFlow.Extensions.Railway

**.NET · MIT**

Dependency-free railway results for C# applications. Works independently of VersionFlow Core, DI, and CQRS.

~~~xml
dotnet add package VersionFlow.Extensions.Railway
~~~

## Start from an existing value

Use `ToRailResult()` to start a chain with type inference:

~~~csharp
using VersionFlow.Extensions.Railway;

var result = "Ada".ToRailResult()
    .EnsureTrue(name => name.Length <= 10, "Name is too long.");
~~~

`value.ToRailResult()` is equivalent to `RailResult<T>.Passed(value)`. It wraps the value as success without validating or cloning it. Explicitly nullable values, including null, are supported. Use `Try` or `TryAsync` instead when creating or loading the value may throw.

## Validate and correct

~~~csharp
using VersionFlow.Extensions.Railway;

var result = RailResult<string>.Passed("A long customer name")
    .EnsureTrue(name => name.Length <= 10,
        "Name must be ten characters or fewer.",
        recover: name => name[..10])
    .EnsureFalse(string.IsNullOrWhiteSpace, "Name is required.");

string message = result.Match(
    passed: name => $"Accepted {name}",
    failed: errors => errors[0].Message);
~~~

EnsureTrue requires the predicate to return true; EnsureFalse requires false. Ensure remains a compatibility alias for EnsureTrue. Both explicit methods stop at the first failure. Recovery belongs to that specific rule: it runs once, then the same predicate checks the corrected value once. A passing rule never invokes recovery. Earlier failures skip subsequent rules and their recovery callbacks. If the initial predicate throws, its exception is captured without attempting recovery.

If correction does not satisfy the rule, the original error remains with RailRecoveryFailure details. If correction or its recheck throws, those details contain the exception. Successful correction replaces the value and leaves no error. Use the per-check passed observer below to learn whether correction occurred.

Prefer immutable records and return corrected copies. Results do not clone values, roll back mutations, or undo external side effects.

## Observe an individual validation check

Use `onPassed` and `onFailed` inside `Ensure`, `EnsureTrue`, or `EnsureFalse` to observe that rule only:

~~~csharp
var validated = "An overly long title".ToRailResult()
    .EnsureFalse(string.IsNullOrWhiteSpace, "Title is required.",
        onFailed: error => Console.WriteLine(error.Message))
    .EnsureTrue(title => title.Length <= 10, "Title is too long.",
        recover: title => title[..10],
        onPassed: (title, wasRecovered) =>
            Console.WriteLine(wasRecovered ? $"Shortened to: {title}" : "Title length is valid."),
        onFailed: error => Console.WriteLine(error.Message));
~~~

| Callback | Receives | Runs when |
| --- | --- | --- |
| `onPassed` | Final value and `wasRecovered` boolean | This rule passes initially or after successful recovery |
| `onFailed` | The final `RailError` | This rule fails, its predicate throws, or recovery/revalidation fails |
| `onPassedAsync` | Final value, recovery boolean, and cancellation token | The same successful outcome, on async validation methods |
| `onFailedAsync` | Final `RailError` and cancellation token | The same failed outcome, on async validation methods |

Callbacks run after this rule's final outcome is known. A rule that recovers successfully calls only its passed observer, with the corrected value and `wasRecovered: true`. The boolean describes this rule, not earlier recovery in the chain. Successful results still have empty `Errors`; no recovery history is stored on the result.

An incoming failure skips the predicate, recovery, and all observers for this rule. This differs from a chained `OnFailure`, which observes any existing failure, even one from an earlier rule. Use per-check `onFailed` for rule-specific logging and `OnFailure` for logging the chain's overall failure.

Observers cannot replace the result. Their exceptions propagate to the caller, including cancellation; they are not captured as validation failures and do not invoke the opposite observer. Exceptions from the predicate remain validation errors, and exceptions from recovery or revalidation remain `RecoveryFailure` details on the error passed to `onFailed`.

### Async validation observers

`EnsureAsync`, `EnsureTrueAsync`, and `EnsureFalseAsync` accept both synchronous observers and the async observer forms. Mix them across outcomes, but supply only one form for each outcome: `onPassed` **or** `onPassedAsync`, and `onFailed` **or** `onFailedAsync`. Supplying both forms for one outcome throws `ArgumentException`, even when an earlier failure would skip the rule.

~~~csharp
var validatedAsync = await "Title".ToRailResult().EnsureTrueAsync(
    (title, ct) => Task.FromResult(title.Length <= 10),
    "Title is too long.",
    onPassed: (title, wasRecovered) => Console.WriteLine($"Accepted: {title}"),
    onFailedAsync: async (error, ct) =>
    {
        await Task.Delay(1, ct); // Replace with application reporting I/O.
        Console.WriteLine(error.Message);
    },
    cancellationToken: cancellationToken);
~~~

The example uses a caller-supplied `cancellationToken`. Async observers receive that token and are awaited before later chain steps run. Callbacks must observe the token during their work. Cancellation is propagated, not reported as a failed validation. A synchronous predicate can be adapted with `Task.FromResult`, as above.

The same observer arguments work when chaining from `Task<RailResult<T>>`. Existing calls without observers retain their validation behavior; the original method signatures remain available to already-compiled consumers.

## Transform and chain

~~~csharp
RailResult<int> SaveName(string name) => RailResult<int>.Passed(42);

var saved = result
    .Select(name => name.Trim())
    .Then(SaveName)
    .OnSuccess(id => Console.WriteLine($"Saved {id}"))
    .OnFailure(errors => Console.WriteLine(errors[0].Message));

var response = saved.Match(
    passed: id => $"Customer {id}",
    failed: errors => errors[0].Message);
~~~

| Member | Behavior |
| --- | --- |
| Passed(value) | Creates success, including an explicitly nullable value |
| Failed(message) | Creates failure with an optional code |
| Failed(error) / Failed(errors) | Creates failure from structured errors |
| Failed(exception) / Failed(message, exception) | Captures an exception with an optional code |
| EnsureTrue(predicate, message, recover, code) | Validates, optionally corrects, then rechecks |
| EnsureTrue(predicate, error, recover) | Validates with a structured error |
| EnsureFalse(predicate, message, recover, code) | Requires false, optionally corrects, then rechecks |
| EnsureFalse(predicate, error, recover) | Requires false with a structured error |
| Ensure(predicate, ...) | Compatibility alias for EnsureTrue |
| Select(selector) | Transforms success into another value (functional Map) |
| Then(operation) | Chains an operation returning a result (functional Bind) |
| When(predicate, operation) | Runs a same-type operation only when the predicate matches |
| WhenAsync(predicate, operation, cancellationToken) | Conditional async operation with a sync or async predicate |
| Branch(predicate, whenTrue, whenFalse) | Chooses one result-returning path on success |
| BranchAsync(predicate, whenTrue, whenFalse, cancellationToken) | Chooses one asynchronous path using a synchronous or asynchronous predicate |
| OnSuccess / OnFailure | Observes one outcome and returns the same result |
| Match(passed, failed) | Handles one outcome and returns an ordinary value |
| IsPassed / IsFailed | Inspects the outcome |
| Value | Reads success; throws InvalidOperationException on failure |
| Errors | Reads an immutable list, empty on success and nonempty on failure |

Select and Then skip callbacks on failure and carry all existing errors forward, even across value types. Then returns the operation's result directly. Null results from Then become exception failures. There is no implicit conversion or implicit unwrapping.

For operations without a response, use RailResult<RailUnit>.Passed(RailUnit.Value).

## Conditional paths

~~~csharp
var processed = RailResult<string>.Passed("  customer  ")
    .Branch(
        name => name.Length > 10,
        whenTrue: name => RailResult<string>.Passed(name.Trim()),
        whenFalse: name => RailResult<string>.Passed(name))
    .Then(SaveName);
~~~

Both paths return the same `RailResult<TNext>` type, which can differ from the input type. Only the selected path runs, and its result is returned directly without another wrapper. An incoming failure skips the predicate and both paths, preserving all errors. Each path can contain its own railway chain. A selected failure skips subsequent success steps.

`BranchAsync` accepts either a synchronous predicate or a Task-returning predicate, with Task-returning paths. It forwards the supplied cancellation token to asynchronous callbacks:

~~~csharp
var processedAsync = await result.BranchAsync(
    name => name.Length > 10,
    whenTrue: (name, ct) => SaveNameAsync(name.Trim(), ct),
    whenFalse: (name, ct) => SaveNameAsync(name, ct),
    cancellationToken: cts.Token);
~~~

Here `SaveNameAsync` returns `Task<RailResult<int>>`. Task extensions allow subsequent steps to be chained before awaiting the final result. Like `ThenAsync`, a pre-cancelled token throws even for an incoming failure. Both branch methods capture predicate and selected-path exceptions except cancellation; null results become exception failures. All three callbacks are required and validated even when skipped.

Custom validation errors and correction callbacks belong to `EnsureTrue`/`EnsureFalse`. `Select` wraps an ordinary value in a successful result; `Then` and branch paths return their own success or custom failure. Use `OnFailure` to observe errors without changing the result, or `Match` to consume either outcome.

## Conditional steps

Use `When` for an optional operation that preserves the current result when skipped:

~~~csharp
var normalized = RailResult<string>.Passed("  customer  ")
    .When(name => name.StartsWith(' '),
        name => RailResult<string>.Passed(name.Trim()))
    .Then(SaveName);
~~~

`Then` runs on every success; `When` additionally requires its predicate to match. A false predicate returns the same result instance. An incoming failure returns unchanged without running either callback. The operation returns `RailResult<T>` with the same value type, and its result is returned directly. A returned failure skips later success steps.

`WhenAsync` accepts a synchronous or asynchronous predicate and an asynchronous operation; cancellation tokens are forwarded to async callbacks. Task extensions support all three forms. As with `Branch`, callback exceptions and null operation results become failures, cancellation propagates, and callbacks are validated even when skipped. A pre-cancelled token throws even for an incoming failure.

## Typed, extensible errors

~~~csharp
public static class CustomerErrorCodes
{
    public static readonly RailErrorCode Duplicate = new("customer.duplicate");
}
~~~

~~~csharp
var failure = RailResult<string>.Failed(
    "This customer already exists.", CustomerErrorCodes.Duplicate);

var validation = RailResult<string>.Passed("")
    .EnsureTrue(name => name.Length > 0, "Name is required.",
        code: new RailErrorCode("customer.name_required"));
~~~

Codes are case-sensitive readonly record structs with value equality. No central enum registration is needed. The default RailErrorCode equals RailErrorCodes.Unspecified. Built-in codes are Unspecified, ValidationFailed, Exception, and RecoveryFailed. Ordinary Failed(message) uses Unspecified; EnsureTrue and EnsureFalse use ValidationFailed; exception factories use Exception. Explicit codes override those defaults.

RailError is a readonly record struct containing Code, Message, optional Exception, and optional RecoveryFailure. RailRecoveryFailure is a readonly record struct containing Code, Message, and optional Exception. There is no target field.

Failed(IEnumerable<RailError>) defensively copies its input and requires at least one valid error. This lets application validation report several errors together; sequential validation calls do not accumulate them. Default error/recovery structs and blank messages are rejected when creating a failure. RailResult<T> is a sealed class with private constructors, preventing an uninitialized default struct from masquerading as a valid outcome.

## Asynchronous operations

~~~csharp
using var cts = new CancellationTokenSource();
var savedAsync = await result.ThenAsync(
    async (name, ct) =>
    {
        await Task.Delay(1, ct); // Replace with application I/O.
        return RailResult<int>.Passed(42);
    },
    cts.Token);

var label = await savedAsync.SelectAsync(
    (id, ct) => Task.FromResult($"Customer {id}"),
    cts.Token);
~~~

Every callback-based operation has an async counterpart:

| Synchronous | Asynchronous |
| --- | --- |
| Ensure / EnsureTrue / EnsureFalse | EnsureAsync / EnsureTrueAsync / EnsureFalseAsync |
| Select / Then / Branch / When | SelectAsync / ThenAsync / BranchAsync / WhenAsync |
| OnSuccess / OnFailure | OnSuccessAsync / OnFailureAsync |
| Match | MatchAsync |
| Recover / MapErrors | RecoverAsync / MapErrorsAsync |

Async methods accept Task-returning callbacks with a CancellationToken. You can await each operation or use task extensions to chain steps and await once at the end. ValueTask-returning APIs can be adapted with AsTask(). A pre-cancelled token throws before callback execution, including when an outcome would skip the callback. Callbacks receive the supplied token and are responsible for observing cancellation during their work. Passed/Failed factories and properties remain synchronous; TryAsync starts an asynchronous operation.

Async validation supports the same message/code or structured error overloads as synchronous validation. Both the predicate and optional recovery callback are asynchronous; recovery runs once and the predicate rechecks the corrected value once. EnsureAsync is the compatibility alias for EnsureTrueAsync. For a synchronous predicate with asynchronous recovery, adapt the predicate with Task.FromResult.

~~~csharp
var validated = await result.EnsureTrueAsync(
    (name, ct) => Task.FromResult(name.Length <= 10),
    message: "Name is too long.",
    recover: (name, ct) => Task.FromResult(name[..10]),
    cancellationToken: cts.Token);

var observed = await validated.OnSuccessAsync(
    (name, ct) => Console.Out.WriteLineAsync(name.AsMemory(), ct),
    cts.Token);
observed = await observed.OnFailureAsync(
    (errors, ct) => Console.Error.WriteLineAsync(errors[0].Message.AsMemory(), ct),
    cts.Token);

var message = await observed.MatchAsync(
    (name, ct) => Task.FromResult($"Accepted {name}"),
    (errors, ct) => Task.FromResult(errors[0].Message),
    cts.Token);
~~~

OnSuccessAsync and OnFailureAsync await only the matching observer and return the same result instance. MatchAsync awaits only the matching handler and returns its ordinary value.

## Starting operations and composing tasks

`RailResult<T>.Try(Func<T>)` wraps an ordinary return value in success and captures exceptions as failures. `TryAsync(Func<CancellationToken, Task<T>>, cancellationToken)` does the same for asynchronous operations. Cancellation propagates. Use Then/ThenAsync when an operation already returns a result, to avoid nesting results.

Every instance chain operation has an extension on `Task<RailResult<T>>`, including synchronous steps, both validation overloads, both async branch predicates, observers, recovery, error mapping, and Match:

~~~csharp
var message = await RailResult<string>.TryAsync(
        ct => File.ReadAllTextAsync("customer.txt", ct), cts.Token)
    .Select(name => name.Trim())
    .EnsureTrue(name => name.Length > 0, "Name is required.")
    .ThenAsync((name, ct) => SaveNameAsync(name, ct), cts.Token)
    .Recover(errors => RailResult<int>.Passed(0))
    .Match(id => $"Customer {id}", errors => errors[0].Message);
~~~

Task extensions await the upstream result before invoking the next step. Faulted upstream tasks propagate their exceptions, including observer exceptions; Recover handles failed results, not faulted tasks. A null upstream result throws InvalidOperationException. Async extension cancellation stops waiting for the upstream task but does not cancel that task's work; pass the token to the original operation too. Tokens are supplied per step, not stored in results.

## Recovering and translating errors

Recover and RecoverAsync run only on a failed result and receive all its errors. The callback returns a replacement `RailResult<T>`, which may itself be a failure. Successful incoming results are returned unchanged. A thrown non-cancellation exception or null fallback result preserves the original errors and appends an exception error. This differs from Ensure recovery, which corrects a value and rechecks a specific rule.

MapErrors and MapErrorsAsync transform each error in input order; async mapping is sequential. The mapper returns one valid RailError for each original error. Successes are unchanged. If mapping throws or produces an invalid error, partial mappings are discarded and the original errors are returned with the exception appended. Cancellation propagates.

To preserve diagnostic details while changing public-facing codes and messages, carry those fields into the replacement:

~~~csharp
var translated = result.MapErrors(error => new RailError(
    "Customer processing failed.",
    new RailErrorCode("customer.processing"),
    error.Exception,
    error.RecoveryFailure));
~~~

Mapping intentionally replaces errors; diagnostic fields are not copied automatically.

## Combining results and processing collections

### Observing completion and retaining partial successes

`OnCompleted` and `OnCompletedAsync` observe either outcome of any `RailResult<T>` and return the same result. The callback receives the entire result. Both also work on `Task<RailResult<T>>`. The async callback is awaited before the chain continues; observer exceptions propagate. These are result observers, not `finally` blocks: upstream task faults and cancellation skip the observer.

Use `RailResult.TraverseResults` or `TraverseResultsAsync` when successful and failed items must both remain available. They attempt every item sequentially and return `RailBatchResult<TInput, TOutput>`; input and output types can differ. No failure-mode argument is needed.

- `Entries`: each original `Input` paired with its `Result`, preserving input order and duplicates.
- `Results`: every individual `RailResult<TOutput>` in input order.
- `SuccessfulValues`: successful output values.
- `Failures`: failed entries with their original inputs and result errors.
- `Errors`: flattened item errors, followed by source enumeration errors.
- `EnumerationErrors`: errors that prevented further source enumeration, without inventing an input item.

All collections are read-only snapshots; input and output objects themselves are not cloned. Empty batches pass. Operation exceptions and null results become item failures and processing continues; enumeration exceptions retain prior outcomes and stop enumeration. Cancellation propagates.

Batch instances and their tasks support `OnFailure`, `OnFailureAsync`, `OnCompleted`, and `OnCompletedAsync`. All receive the full batch and preserve its identity. Failure observers run when any item or enumeration failed; completion observers run for either batch outcome.

~~~csharp
var cleanupResults = await RailResult.TraverseResultsAsync(
    groupsToCleanup,
    (group, ct) => DeleteGroupAsync(group.ID, ct),
    cancellationToken)
    .OnFailure(batch => NotifyFailures(batch.Failures.Count, batch.Errors))
    .OnCompletedAsync(async (batch, ct) =>
    {
        // Use original inputs even when DeleteGroupAsync returns another payload type.
        var deletedGroups = batch.Entries
            .Where(entry => entry.Result.IsPassed)
            .Select(entry => entry.Input)
            .ToList();

        await BulkRemoveAsync(deletedGroups, ct);
        // Failed entries remain available in batch.Failures for retry.
    }, cancellationToken);

// Every delete and the bulk removal have completed before proceeding.
// Process the independent add batch here.
~~~

`Failures.Count` counts failed items; `Errors.Count` counts errors and may be larger. Enumeration errors do not count as failed items. Cancellation tokens are forwarded to async callbacks; canceling a task-extension wait does not cancel upstream work. Pass the token to traversal as well.

### Combining into one success or failure

| Operation | Default failure mode |
| --- | --- |
| RailResult.Combine(results) / CombineAsync(tasks) | CollectAll |
| RailResult.Traverse(items, operation) / TraverseAsync(items, operation) | StopOnFirstFailure |

Both modes preserve input order and return `RailResult<IReadOnlyList<T>>`. Empty inputs succeed with an empty list. Any failure yields errors without a partial success list. StopOnFirstFailure preserves all errors from the first failed item and stops enumeration. CollectAll collects every item's errors in order.

~~~csharp
var validations = RailResult.Combine(new[]
{
    RailResult<string>.Passed("Ada").EnsureTrue(name => name.Length > 0),
    RailResult<string>.Passed("").EnsureTrue(name => name.Length > 0)
});

var saved = await RailResult.TraverseAsync(
    new[] { "Ada", "Grace" },
    (name, ct) => SaveNameAsync(name, ct),
    failureMode: RailFailureMode.CollectAll,
    cancellationToken: cts.Token);
~~~

Traversal invokes operations sequentially, including async traversal, and checks cancellation between items. Operation exceptions and null results become item failures; cancellation propagates. An enumeration exception terminates processing in either mode and is appended to errors already collected.

CombineAsync awaits supplied tasks in input order and captures their faults as item errors. It does not cancel already-started tasks. With early stopping or cancellation, later tasks are not awaited; callers retain responsibility for those tasks. Prefer TraverseAsync when the library should control when each operation starts.

## Result and side-effect contract

A result's outcome and error collection are immutable, but its successful value may reference a mutable object. Results do not clone values, roll back mutations, or undo external side effects. Observers return the same result and may throw. Cancellation is control flow and propagates rather than becoming a failure. Recovery, mapping, and traversal do not automatically retry operations or establish transactions.

## Exceptions and cancellation

EnsureTrue, EnsureFalse, Ensure, Select, Then, Branch, When, and their async counterparts capture callback exceptions as errors. Recovery exceptions attach to the original validation error instead of replacing it. OperationCanceledException (including TaskCanceledException) always propagates from callbacks. Explicit Failed(exception) factories store exactly the exception provided, including cancellation exceptions if deliberately supplied.

OnSuccess, OnFailure, Match, and their async counterparts propagate exceptions: these callbacks observe or consume the outcome and do not silently replace it. Invalid API arguments also throw, even when a step would otherwise be skipped.

Exception objects are intended for diagnostics. Use Match to choose safe messages and response types for public HTTP responses.

## Build and package

~~~sh
dotnet test VersionFlow.slnx -c Release
dotnet pack VersionFLow.Extensions.Railway/VersionFLow.Extensions.Railway.csproj -c Release -o artifacts/packages
~~~

Uses the repository's shared version, author, MIT license metadata, README, and package icon. The package has no runtime dependencies.

MIT. Copyright (c) 2026 Bradley Ables. Full license included as LICENSE.
