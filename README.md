# VersionFlow

**1.0.0 · .NET 10+**

VersionFlow selects service and method implementations from configuration while preserving ordinary typed C# calls and existing DI lifetimes. DI creates implementations; VersionFlow selects and dispatches versions.

## Projects and packages

- `VersionFlow.Core`: attributes, immutable catalogs, options, selection, fluent APIs, and the optional Microsoft DI adapter. Targets `net10.0`.
- `VersionFlow.Generator`: incremental Roslyn generator, bundled as an analyzer inside the Core package. This development project is not separately published. Targets `netstandard2.0` for compiler compatibility, not as a supported application runtime.
- `samples/VersionFlow.SampleWeb`: class routing, independent methods, interface injection, explicit selection, and incompatible-signature callbacks.
- `tests/VersionFlow.Tests`: runtime, generator, and integration tests. `VersionFlow.TestServices` exercises cross-assembly catalog linking.

Install Core into projects declaring versioned implementations; its package includes the generator:

```xml
<PackageReference Include="VersionFlow.Core" Version="1.0.0" />
```

For source project references, mark the generator `OutputItemType="Analyzer" ReferenceOutputAssembly="false"`.

## One versioning attribute

All Core version declarations use `VersionedAttribute` (written `[Versioned(...)]`):

| Target | Declaration |
| --- | --- |
| Class implementation version | `[Versioned(typeof(IContract), 1, Key = "Service")]` |
| Independent operations host | `[Versioned(typeof(IContract), Key = "Operations")]` |
| Method inside that host | `[Versioned(nameof(IContract.MethodName), 1)]` |

A class version supplies a positive integer; a host omits the version because each operation has its own versions. Methods specify the operation name and positive version. `Key` is optional on classes only. These are three constructor overloads of the same attribute; incorrect target/overload combinations produce compiler diagnostics. DI registration continues to use the separate `[Service]` attribute.
## Class versions

```csharp
using VersionFlow;

public interface IPricing
{
    decimal Calculate(int quantity);
    decimal Discount(decimal amount);
}

[Versioned(typeof(IPricing), 1, Key = "Pricing")]
public sealed class PricingV1 : IPricing
{
    public decimal Calculate(int quantity) => quantity * 10m;
    public decimal Discount(decimal amount) => amount * 0.95m;
}

[Versioned(typeof(IPricing), 2, Key = "Pricing")]
public sealed class PricingV2 : IPricing
{
    public decimal Calculate(int quantity) => quantity * 9m;
    public decimal Discount(decimal amount) => amount * 0.90m;
}
```

Implementations can have different constructor dependencies. Register concrete types through your existing DI setup:

```csharp
builder.Services.AddScoped<PricingV1>();
builder.Services.AddScoped<PricingV2>();
// Namespace derives from the assembly name; this example assumes MyApp.
builder.Services.AddVersionFlowCatalog<
    VersionFlow.Generated.Assembly_MyApp.GeneratedCatalog>();
builder.Services.AddVersionFlow(builder.Configuration.GetSection("VersionFlow"));
builder.Services.AddVersionFlowDispatchers();
```

Link catalogs before registering dispatchers. There are no per-version VersionFlow registrations. Ordinary concrete registrations, including those installed by external registration libraries, retain their original lifetimes. Interface-only registrations are not sufficient to select individual class versions. There is no implicit activation fallback.

```json
{
  "VersionFlow": {
    "Default": "Latest",
    "Classes": { "Pricing": "1" },
    "Methods": { "Pricing.Calculate": "2" },
    "ValidateOnStart": true,
    "MissingVersionBehavior": "Throw"
  }
}
```

Inject `IPricing` normally. `Calculate` dispatches to v2 while `Discount` dispatches to v1, within the current scope. Only the implementation needed for that call is resolved. Injecting a concrete type intentionally bypasses routing. Attributes do not intercept direct calls.

`AddVersionFlowDispatchers()` is optional; fluent `Resolve()` works without it. The adapter registers scoped facades, rejects existing unkeyed contract registrations, and checks for subsequently added conflicts at host startup. Register dispatchers after other DI registration libraries. Enable standard DI scope validation; resolve facades and `IVersionFlow` within a scope. Do not inject them into singletons.

## Options and reload

All three setup styles are supported:

```csharp
services.AddVersionFlow(configuration.GetSection("VersionFlow"));
services.AddVersionFlow(options => options.Classes["Pricing"] = "1");
services.AddVersionFlowOptions()
    .Bind(configuration.GetSection("VersionFlow"))
    .ValidateOnStart();
```

Choose one style. Additional `Configure`, `PostConfigure`, and `IValidateOptions<VersionFlowOptions>` registrations participate normally. Runtime service and dispatcher installation is idempotent; configuration delegates themselves follow normal options semantics and accumulate when added repeatedly.

Selection precedence is **explicit choice → method → class → global**. `Latest` resolves independently against each operation's available versions. Values must be exactly `Latest` or positive integer strings; numeric JSON values also bind. Keys are ordinal, case-sensitive. The fallback key is the fully qualified contract type name. Missing versions never fall back.

Startup validation is enabled by default in applications using the generic/web host. `ValidateOnStart=false` defers VersionFlow validation until first use; an explicit options-builder `.ValidateOnStart()` still takes effect. With a standalone `ServiceProvider`, first resolution validates configuration. Host startup additionally checks concrete registrations without constructing implementations.

Reload-capable configuration providers update subsequent facade calls. VersionFlow builds and validates a complete immutable candidate, publishes it atomically, and logs/rejects invalid updates while retaining the previous snapshot. `IOptions`, `IOptionsSnapshot`, and `IOptionsMonitor` remain available with their standard Microsoft behavior. An application independently reading `IOptionsMonitor.CurrentValue` can still get `OptionsValidationException` after invalid configuration; the VersionFlow dispatch snapshot remains usable. VersionFlow subscribes to options change tokens directly because monitor callbacks are not invoked for rejected values.

## Independent method versions

```csharp
public interface IPricingOperations { decimal Calculate(int quantity); }

[Versioned(typeof(IPricingOperations), Key = "PricingOps")]
public sealed class PricingOperations
{
    [Versioned("Calculate", 1)]
    public decimal Original(int quantity) => quantity * 10m;

    [Versioned("Calculate", 3)]
    public decimal Newest(int quantity) => quantity * 9m;
}
```

Register `PricingOperations` as a concrete type. Its generated facade implements `IPricingOperations`. Different operations may declare different available version sets. `Classes["PricingOps"]` supplies their fallback choice, and `Methods["PricingOps.Calculate"]` overrides it. An explicit numeric choice must exist on every method actually called. Class families and independent operations cannot share a contract/key.

## Fluent resolution and introspection

```csharp
var configured = flow.For<IPricing>().Resolve();
var original = flow.For<IPricing>().UseVersion(1).Resolve();
var newest = flow.For<IPricing>().UseLatest().Resolve();
var selection = flow.Select("Pricing", "Calculate");
```

`Resolve()` always returns a generated facade. Explicit choices apply to all its calls, overriding method configuration. Builders are immutable and reusable; they retain their originating scope and must not outlive it. A configured facade may select different versions between calls.

`VersionSelection` contains the key, method, number, source, immutable available versions, and configuration snapshot identity. Operations hosts require a method for introspection because they have no single class version. A separate selection query and later invocation are not atomic across reloads. Each individual typed invocation or fluent `Execute` chooses and executes using one captured selection.

## Incompatible signatures

Declare the operation name once so startup validation recognizes configuration for it; callback branches declare version availability at execution:

```csharp
services.AddVersionFlowOperation("Orders.Process");

var result = await flow.For("Orders.Process").MatchAsync<OrderResult>()
    .Version<OrderV1>(1, service => service.Process(orderId))
    .Version<OrderV2>(2, (service, ct) => service.Process(orderId, true, ct))
    .ExecuteAsync(cancellationToken);
```

Register callback service types normally; interfaces are permitted when they identify the intended service unambiguously. Only the selected callback's service is resolved and only that callback executes. `.UseVersion(n)` and `.UseLatest()` are available before matching. Synchronous execution uses `Match<TResult>().Version<TService>(...).Execute()`. Duplicate/non-positive branches are rejected; unavailable selections fail descriptively. Exceptions propagate naturally. Cancellation is checked before dispatch and passed to token-aware callbacks. Tokenless callbacks cannot be forcibly canceled. For `ValueTask`, explicitly adapt with `.AsTask()` to avoid ambiguous lambda overloads.

Fluent-only version availability cannot be validated at startup because callbacks may be constructed dynamically. Unknown keys still fail startup validation; `AddVersionFlowOperation` supplies only known names, not a hand-maintained version switch table.

## Cross-assembly linking and limitations

Each declaring assembly produces `VersionFlow.Generated.Assembly_<sanitized assembly name>.GeneratedCatalog`, replacing non-alphanumeric assembly-name characters with underscores. Link each catalog explicitly using `AddVersionFlowCatalog<T>()`. Referenced assemblies are not scanned, and the generator does not rediscover their declarations. Catalog keys/contracts are checked for collisions when combined. Avoid assembly names that sanitize to the same generated namespace. Add the analyzer directly to each project that declares implementations.

Generated dispatch uses direct typed calls, supports explicit interface implementations, inherited ordinary interface methods, nullable references, `void`, `Task`, `ValueTask`, and ordinary parameter/return types. v1 reports compiler errors for generic contracts/implementation types/methods, overloaded or hidden operation names, `ref`/`out`/`in` signatures, ref returns, properties/indexers/events, static/default interface members, pointer signatures, inaccessible implementations, missing public constructors, missing methods, duplicate versions, invalid keys, and mode conflicts. Operation attributes belong on methods declared on the operations host itself.

Dispatch does not use reflection or dynamic proxies. Explicit catalog linking is friendly to trimming, but Native AOT is not certified in this release: configuration binding and the application's DI registrations may require their own AOT setup. There is no runtime assembly discovery, implicit unregistered construction, version matrix, or automatic adaptation of incompatible signatures.

Stateful versions maintain separate state. Switching versions between calls does not transfer state; prefer stateless services when routing can change.

## Build, test, pack, and run

```powershell
dotnet build VersionFlow.slnx -c Release
dotnet test VersionFlow.slnx -c Release
dotnet pack VersionFlow.slnx -c Release -o artifacts/packages
dotnet run --project samples/VersionFlow.SampleWeb --urls http://localhost:5129
```

Sample routes:

- `/` returns class and independent-operation results. The configured values are 95, 95, 95, and 95.
- `/explicit/1` returns 100.
- `/orders/42` returns `Order 42: v2, validate=True`.

See [DESIGN.md](DESIGN.md) for implementation choices and diagnostics.

## Optional attribute DI extension

`VersionFlow.Extensions.DI` works independently of both Core and CQRS. It uses Microsoft's `ServiceLifetime` directly, supporting scoped (the default), singleton, and transient:

```csharp
using Microsoft.Extensions.DependencyInjection;
using VersionFlow.Extensions.DI;

[Service] // Concrete registration only, scoped
public sealed class PriceRepository { }

[Service(typeof(IClock), ServiceLifetime.Singleton)]
public sealed class Clock : IClock { }

[Service(ServiceLifetime.Transient, typeof(IReportBuilder), typeof(IReportExporter))]
public sealed class ReportBuilder : IReportBuilder, IReportExporter { }

services.AddAttributedServices(); // Immediate calling assembly
// Or select assemblies explicitly:
services.AddAttributedServices(typeof(PriceRepository).Assembly);
services.AddAttributedServicesFromAssemblyContaining<PriceRepository>();
```

Every marked class is registered by concrete type. Explicit interface/base-type bindings can be specified with either `[Service(typeof(IContract))]` (scoped) or `[Service(ServiceLifetime.Scoped, typeof(IContract), ...)]`. Each contract must be assignable from the implementation. Each concrete, interface, and base-type binding is a native DI type registration with its own instance. Scoped bindings reuse their instance within a scope, singleton bindings reuse theirs within a provider, and transient resolutions create new instances. Different bindings do not share instance identity or service-local state.

To opt into binding all implemented interfaces:

```csharp
[Service(ServiceLifetime.Scoped, AsImplementedInterfaces = true)]
public sealed class MyService : IMyService, IAnotherService { }
```

This includes inherited and closed generic interfaces. Explicit bindings and discovered interfaces are combined and deduplicated. `IDisposable` and `IAsyncDisposable` are excluded from automatic interface discovery; those are lifecycle interfaces, not application service contracts. Discovery defaults to false so incidental interfaces do not become bindings. Attribute inheritance is disabled; mark each implementation to be registered.

Repeated scans are idempotent. Matching existing service-type/implementation-type/lifetime registrations are reused. Conflicting registrations or multiple implementations claiming the same contract fail explicitly rather than relying on last-registration-wins. For deliberate `IEnumerable<T>` multi-registration, use ordinary DI registrations instead. All declarations are checked before appending registrations; scan failures do not leave a partially modified collection. No service is constructed while scanning.

Attributed types must be closed concrete classes with public constructors; open generic registrations should use native DI directly. The scanner uses reflection at startup and declares trimming warnings. Explicit assembly arguments are recommended from shared bootstrap helpers; parameterless calls scan the immediate caller, not all loaded assemblies. Loader errors fail with dependency details instead of silently skipping services. Normal Microsoft DI scope validation and disposal semantics apply. The scanner uses implementation-type registrations, not forwarding factories, so it never registers the same disposable object under multiple bindings. Each constructed instance has a single DI owner and is disposed once by its owning scope/provider. Use asynchronous scope/provider disposal for IAsyncDisposable-only implementations; for implementations supporting both disposal interfaces, asynchronous disposal prefers DisposeAsync.

For versioned families, use `[Service]` without interface discovery or the shared versioned contract. VersionFlow registers that contract's dispatcher separately:

```csharp
[Service]
[Versioned(typeof(IPricing), 1, Key = "Pricing")]
public sealed class PricingV1 : IPricing { /* implementation */ }

services.AddAttributedServices();
services.AddVersionFlowCatalog<VersionFlow.Generated.Assembly_MyApp.GeneratedCatalog>();
services.AddVersionFlow(configuration.GetSection("VersionFlow"));
services.AddVersionFlowDispatchers();
```

## Optional mini CQRS extension

`VersionFlow.Extensions.Cqrs` is a dependency-free contracts package porting MicroCqrs's direct-call handler model. There is no handler registry, CQRS-specific registration attribute, mediator, dispatcher pipeline, or request base class. It references neither the DI extension nor VersionFlow.Core. Inject a concrete handler or typed handler interface and call it directly; applications can also construct handlers themselves.

When using the separate DI extension, bind handler interfaces like any other interface:

```csharp
using VersionFlow.Extensions.Cqrs;
using VersionFlow.Extensions.DI;

public sealed record GetPrice(int Quantity);

[Service(typeof(ICqrsHandler<GetPrice, decimal>))] // Scoped
public sealed class GetPriceHandler(IPricing pricing) : ICqrsHandler<GetPrice, decimal>
{
    public Task<decimal> HandleAsync(GetPrice request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(pricing.Calculate(request.Quantity));
    }
}

services.AddAttributedServices();
```

Alternatively, `[Service(ServiceLifetime.Scoped, AsImplementedInterfaces = true)]` binds a handler's implemented interfaces, including multiple different CQRS contracts. Or omit attributes entirely and use ordinary DI:

```csharp
services.AddScoped<ICqrsHandler<GetPrice, decimal>, GetPriceHandler>();
```

`ICqrsHandler<TRequest>` returns `Task`; `ICqrsHandler<TRequest, TResponse>` returns `Task<TResponse>`. Both accept an optional cancellation token. Handling, cancellation, and exception behavior belong to the implementation; the interfaces introduce no interception. The request parameter is contravariant in C#, but native DI still requires a registration for the exact service type being requested.

Migration from MicroCqrs changes `using MicroCqrs` to `using VersionFlow.Extensions.Cqrs`. Handler method signatures stay the same. Replace `[CqrsHandler(...)]` with general `[Service(...)]` bindings and `AddCqrsHandlers(...)` with `AddAttributedServices(...)`, or register with your preferred DI system. These CQRS-specific registration APIs are intentionally absent.

The web sample uses attribute DI and exposes `/cqrs/10`, returning `95` through a directly injected CQRS handler and versioned pricing facade. Both extensions target .NET 10 and inherit version `1.0.0`.


## License and package assets

MIT. Copyright (c) 2026 Bradley Ables. See `LICENSE` at the solution root or inside each published project/package.

All three published projects include their own README and MIT license, plus the shared `assets/VersionFlow-icon.png` embedded as the NuGet package icon. `Directory.Build.props` centralizes version 1.0.0 and metadata; `Directory.Build.targets` packs the project-local documentation/license and shared icon. The icon was losslessly recompressed from the supplied image without changing pixel data.





Fluent `.For("Key.Method")` names must be declared with `AddVersionFlowOperation` or present in a linked generated catalog. Names are case-sensitive. Unknown names throw `VersionFlowSelectionException` immediately, including when an explicit version is requested; they never silently inherit the global default.

