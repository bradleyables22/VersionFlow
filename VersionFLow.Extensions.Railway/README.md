# VersionFlow.Extensions.Railway

**1.0.1 · .NET 10+ · MIT**

Dependency-free railway results for C# applications. Works independently of VersionFlow Core, DI, and CQRS.

~~~xml
<PackageReference Include="VersionFlow.Extensions.Railway" Version="1.0.1" />
~~~

## Validate and correct

~~~csharp
using VersionFlow.Extensions.Railway;

var result = RailResult<string>.Passed("A long customer name")
    .Ensure(name => name.Length <= 10,
        "Name must be ten characters or fewer.",
        recover: name => name[..10])
    .Ensure(name => !string.IsNullOrWhiteSpace(name), "Name is required.");

string message = result.Match(
    passed: name => $"Accepted {name}",
    failed: errors => errors[0].Message);
~~~

Ensure stops at the first failure. Recovery belongs to that specific rule: it runs once, then the same predicate checks the corrected value once. A passing rule never invokes recovery. Earlier failures skip subsequent rules and their recovery callbacks. If the initial predicate throws, its exception is captured without attempting recovery.

If correction does not satisfy the rule, the original error remains with RailRecoveryFailure details. If correction or its recheck throws, those details contain the exception. Successful correction replaces the value and leaves no error.

Prefer immutable records and return corrected copies. Results do not clone values, roll back mutations, or undo external side effects.

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
| Ensure(predicate, message, recover, code) | Validates, optionally corrects, then rechecks |
| Ensure(predicate, error, recover) | Validates with a structured error |
| Select(selector) | Transforms success into another value (functional Map) |
| Then(operation) | Chains an operation returning a result (functional Bind) |
| OnSuccess / OnFailure | Observes one outcome and returns the same result |
| Match(passed, failed) | Handles one outcome and returns an ordinary value |
| IsPassed / IsFailed | Inspects the outcome |
| Value | Reads success; throws InvalidOperationException on failure |
| Errors | Reads an immutable list, empty on success and nonempty on failure |

Select and Then skip callbacks on failure and carry all existing errors forward, even across value types. Then returns the operation's result directly. Null results from Then become exception failures. There is no implicit conversion or implicit unwrapping.

For operations without a response, use RailResult<RailUnit>.Passed(RailUnit.Value).

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
    .Ensure(name => name.Length > 0, "Name is required.",
        code: new RailErrorCode("customer.name_required"));
~~~

Codes are case-sensitive readonly record structs with value equality. No central enum registration is needed. The default RailErrorCode equals RailErrorCodes.Unspecified. Built-in codes are Unspecified, ValidationFailed, Exception, and RecoveryFailed. Ordinary Failed(message) uses Unspecified; Ensure uses ValidationFailed; exception factories use Exception. Explicit codes override those defaults.

RailError is a readonly record struct containing Code, Message, optional Exception, and optional RecoveryFailure. RailRecoveryFailure is a readonly record struct containing Code, Message, and optional Exception. There is no target field.

Failed(IEnumerable<RailError>) defensively copies its input and requires at least one valid error. This lets application validation report several errors together; sequential Ensure calls do not accumulate them. Default error/recovery structs and blank messages are rejected when creating a failure. RailResult<T> is a sealed class with private constructors, preventing an uninitialized default struct from masquerading as a valid outcome.

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

SelectAsync and ThenAsync accept Task-returning callbacks with a CancellationToken. Await each operation before continuing the chain. ValueTask-returning APIs can be adapted with AsTask(). A pre-cancelled token throws before callback execution, including for an already failed result. Callbacks receive the supplied token and are responsible for observing cancellation during their work.

## Exceptions and cancellation

Ensure, Select, Then, SelectAsync, and ThenAsync capture callback exceptions as errors. Recovery exceptions attach to the original validation error instead of replacing it. OperationCanceledException (including TaskCanceledException) always propagates from callbacks. Explicit Failed(exception) factories store exactly the exception provided, including cancellation exceptions if deliberately supplied.

OnSuccess, OnFailure, and Match exceptions propagate: these callbacks observe or consume the outcome and do not silently replace it. Invalid API arguments also throw, even when a step would otherwise be skipped.

Exception objects are intended for diagnostics. Use Match to choose safe messages and response types for public HTTP responses.

## Build and package

~~~sh
dotnet test VersionFlow.slnx -c Release
dotnet pack VersionFLow.Extensions.Railway/VersionFLow.Extensions.Railway.csproj -c Release -o artifacts/packages
~~~

Uses the repository's shared version, author, MIT license metadata, README, and package icon. The package has no runtime dependencies.

MIT. Copyright (c) 2026 Bradley Ables. Full license included as LICENSE.
