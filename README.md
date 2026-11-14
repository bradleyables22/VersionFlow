# VersionFlow

**1.0.1 · .NET 10+ · MIT**

Choose class and method versions through configuration while calling ordinary typed interfaces. DI creates your implementations; VersionFlow selects which one receives each call.

## Install

Install Core in your application and every project declaring versioned classes. Its NuGet package includes the source generator; there is no separate generator package.

```xml
<PackageReference Include="VersionFlow.Core" Version="1.0.1" />
```

Optional packages:

```xml
<PackageReference Include="VersionFlow.Extensions.DI" Version="1.0.1" />
<PackageReference Include="VersionFlow.Extensions.Cqrs" Version="1.0.1" />
<PackageReference Include="VersionFlow.Extensions.Railway" Version="1.0.1" />
<PackageReference Include="VersionFlow.Extensions.Results" Version="1.0.1" />
<PackageReference Include="VersionFlow.Extensions.Results.AspNetCore" Version="1.0.1" />
```

DI provides `[Service]` registration. CQRS provides direct-call handler interfaces. Railway provides typed results, validation with optional recovery, and fluent operation chaining. All three work independently of Core and each other. See [the Railway README](VersionFLow.Extensions.Railway/README.md) for usage and exception semantics.

For plain operation outcomes and numbered, offset, or cursor pages, see [Results](VersionFlow.Extensions.Results/README.md). The optional [ASP.NET Core adapter](VersionFlow.Extensions.Results.AspNetCore/README.md) maps them to HTTP responses. Results works independently of Core and Railway.


## Quick start: versioned CQRS handler

A Versioned contract must be non-generic. For CQRS, give the closed generic handler interface a named wrapper:

```csharp
using VersionFlow;
using VersionFlow.Extensions.Cqrs;
using VersionFlow.Extensions.DI;

public sealed record GetPrice(int Quantity);
public interface IGetPriceHandler : ICqrsHandler<GetPrice, decimal> { }

[Service]
[Versioned(typeof(IGetPriceHandler), 1, Key = "Pricing")]
public sealed class GetPriceV1 : IGetPriceHandler
{
    public Task<decimal> HandleAsync(GetPrice request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(request.Quantity * 10m);
    }
}

[Service]
[Versioned(typeof(IGetPriceHandler), 2, Key = "Pricing")]
public sealed class GetPriceV2 : IGetPriceHandler
{
    public Task<decimal> HandleAsync(GetPrice request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(request.Quantity * 9m);
    }
}
```

For implementations in your application assembly, startup is just:

```csharp
builder.Services.AddAttributedServices();
builder.Services.AddVersionFlow(builder.Configuration.GetSection("VersionFlow"));
```

`appsettings.json`:

```json
{
  "VersionFlow": {
    "Default": "Latest",
    "Classes": { "Pricing": "2" }
  }
}
```

Inject the wrapper and call normally:

```csharp
public sealed class Checkout(IGetPriceHandler pricing)
{
    public Task<decimal> Total(int quantity, CancellationToken ct)
        => pricing.HandleAsync(new GetPrice(quantity), ct);
}
```

This calls GetPriceV2. Switching `Classes:Pricing` to `"1"` selects GetPriceV1. Register each constructor dependency through your usual DI setup too.

**What the two startup calls do:** AddAttributedServices registers concrete implementations. AddVersionFlow reads generated assembly metadata, links available catalogs, installs the runtime, and registers scoped interface dispatchers. You do not need to name a generated catalog or call AddVersionFlowDispatchers for normal setup.

Use `[Service]` alone on versioned implementations. Binding their shared interface through `[Service(typeof(IGetPriceHandler))]` or automatic interface discovery would conflict with the dispatcher. Do not pass `typeof(ICqrsHandler<GetPrice, decimal>)` directly to Versioned: even a closed generic contract is unsupported. Its non-generic wrapper and inherited HandleAsync method are supported.

## Without CQRS or attribute DI

Any supported non-generic interface can be versioned. CQRS is optional. For ordinary DI, replace AddAttributedServices with concrete registrations:

```csharp
builder.Services.AddScoped<GetPriceV1>();
builder.Services.AddScoped<GetPriceV2>();
builder.Services.AddVersionFlow(options => options.Classes["Pricing"] = "2");
```

There are no per-version VersionFlow registration calls. Existing transient, scoped, and singleton implementation lifetimes remain intact. Implementations must be registered by concrete type; VersionFlow never silently constructs unregistered services.

## Selection and method overrides

Precedence is **explicit choice → method configuration → class configuration → global default**. Versions are positive integers. `Latest` means the highest available version for that operation. Keys are case-sensitive; omitted keys default to the fully qualified contract name.

For a class family implementing Calculate and Discount:

```json
{
  "VersionFlow": {
    "Classes": { "Pricing": "1" },
    "Methods": { "Pricing.Calculate": "2" }
  }
}
```

Calculate calls the v2 class; Discount calls the v1 class. For the CQRS example, the method key is `Pricing.HandleAsync`.

## Independently versioned methods

```csharp
public interface IPricingOperations { decimal Calculate(int quantity); }

[Service]
[Versioned(typeof(IPricingOperations), Key = "PricingOps")]
public sealed class PricingOperations
{
    [Versioned(nameof(IPricingOperations.Calculate), 1)]
    public decimal Original(int quantity) => quantity * 10m;

    [Versioned(nameof(IPricingOperations.Calculate), 3)]
    public decimal Newest(int quantity) => quantity * 9m;
}
```

Use the same two startup calls. Inject IPricingOperations. Omitting the version on the class marks an operations host; each annotated method declares its own version. Different operations can have different version sets. Class-family and independent-method models cannot share one contract/key.

## Inspect or explicitly select a version

Inject IVersionFlow within a scope:

```csharp
var selected = flow.Select("Pricing", "HandleAsync");
int number = selected.Number;

var original = flow.For<IGetPriceHandler>().UseVersion(1).Resolve();
var newest = flow.For<IGetPriceHandler>().UseLatest().Resolve();
```

Resolve returns a generated facade. Explicit choices override all method configuration on that facade. VersionSelection includes the key, method, number, selection source, available versions, and snapshot identity. Independent operations hosts require a method for introspection.

## Fluent branches for incompatible signatures

When versions take different arguments, explicitly describe each call. First declare the fluent-only name and register the callback service types normally:

```csharp
services.AddVersionFlowOperation("Orders.Process");

var result = await flow.For("Orders.Process").MatchAsync<OrderResult>()
    .Version<OrderV1>(1, service => service.Process(orderId))
    .Version<OrderV2>(2, (service, ct) => service.Process(orderId, true, ct))
    .ExecuteAsync(cancellationToken);
```

Only the selected service is resolved and callback executed. Every callback must produce Task<OrderResult>; adapt differing return types in the callback. Synchronous branches use Match<TResult>() and Execute(). UseVersion/UseLatest before Match overrides configuration. Adapt ValueTask with AsTask if needed. Callback exceptions propagate; cancellation is passed to token-aware callbacks.

AddVersionFlowOperation declares a configuration name, not versions or handlers. Generated operations such as `Pricing.HandleAsync` already have known names and need no extra declaration. Unknown/misspelled names fail at For before any callback executes. Fluent branch availability is checked at execution, because branches can be built dynamically.

## Options, reload, and scopes

Choose one setup style:

```csharp
services.AddVersionFlow(configuration.GetSection("VersionFlow"));
services.AddVersionFlow(options => options.Classes["Pricing"] = "1");
services.AddVersionFlowOptions().Bind(configuration.GetSection("VersionFlow")).ValidateOnStart();
services.AddVersionFlow(); // Defaults to Latest
```

All automatically register catalogs and dispatchers. Configure, PostConfigure, IValidateOptions, IOptions, IOptionsSnapshot, and IOptionsMonitor use the normal options infrastructure. Repeated setup does not duplicate runtime/facade registrations; added configuration delegates still accumulate normally.

Startup validation defaults to enabled in generic/web hosts. Set ValidateOnStart=false to defer VersionFlow validation until first use; explicit options-builder ValidateOnStart still applies. Only MissingVersionBehavior="Throw" is supported. Missing versions never silently fall back.

Reload-capable providers update subsequent calls. Invalid updates are logged and rejected while dispatch retains its last valid immutable snapshot. Native options consumers can still throw validation exceptions for invalid configuration. Each invocation uses one snapshot; a separate introspection query followed by a call is not atomic. State is not transferred between implementation versions.

Facades and IVersionFlow are scoped. Do not inject them into singletons or use builders after their scope ends. Enable native DI scope validation. Injecting a concrete version bypasses selection deliberately.

## Multiple projects and shared startup helpers

The generator emits catalog metadata for each assembly and typed links to referenced generated catalogs. AddVersionFlow starts at its immediate calling assembly, traverses those links, and deduplicates catalogs. It does not scan arbitrary loaded assemblies or plugin directories. Install Core (with its bundled analyzer) in the startup project and all projects declaring versioned types, and rebuild them for 1.0.1.

DI registration is separate. Include implementation assemblies when using the attribute scanner:

```csharp
builder.Services.AddAttributedServices(typeof(Program).Assembly, typeof(GetPriceV1).Assembly);
builder.Services.AddVersionFlow(builder.Configuration.GetSection("VersionFlow"));
```

From a shared startup helper, specify the intended discovery root if it is not the helper's assembly:

```csharp
services.AddVersionFlow(configuration.GetSection("VersionFlow"), assembly: typeof(GetPriceV1).Assembly);
```

Use the startup assembly as root when it brings together several handler libraries. All discovered concrete implementations need DI registration, including currently inactive versions, for startup validation.

## Advanced/manual setup and upgrading from 1.0.0

For normal 1.0.1 usage, remove explicit AddVersionFlowCatalog and AddVersionFlowDispatchers calls. Keep concrete DI registration and AddVersionFlow. If you deliberately register catalogs before setup, those explicit catalogs define the discovery scope and are respected; use the fully automatic path to include referenced catalogs automatically.

For fluent-only integration or applications that own interface registrations themselves:

```csharp
services.AddVersionFlowCatalog<VersionFlow.Generated.Assembly_MyApp.GeneratedCatalog>();
services.AddVersionFlow(configuration.GetSection("VersionFlow"), autoRegister: false);
// Optional only in this manual path:
services.AddVersionFlowDispatchers();
```

AddVersionFlowOptions also accepts autoRegister:false. This disables discovery and facade registration, not options/runtime installation. Manually link each needed catalog. AddVersionFlowDispatchers registers facades from linked catalogs and rejects conflicting unkeyed interface registrations. Host startup checks later conflicts when startup validation is enabled. Existing registrations are never silently replaced.

## Limitations

Generated contracts must be non-generic. Inherited methods from closed generic interfaces are supported through a non-generic wrapper. Generic methods/implementation types, overloaded operation names, ref/out/in signatures, ref returns, properties/indexers/events, static/default interface methods, and pointer signatures are rejected with diagnostics. Attributes do not intercept direct calls or rewrite existing code.

Normal dispatch uses direct generated calls. Startup discovery reads generated assembly metadata and constructs catalog metadata only; it does not activate service implementations. Native AOT is not certified. Attribute DI scanning requires preserving metadata when trimming.

The DI extension uses native registrations per binding: interface and concrete bindings have separate scoped/singleton instances, and each constructed disposable instance has one DI owner. For shared state, inject a shared dependency rather than assuming different bindings return the same object.

## Build and release

```sh
dotnet build VersionFlow.slnx -c Release
dotnet test VersionFlow.slnx -c Release --no-build
dotnet pack VersionFlow.slnx -c Release --no-build -o artifacts/packages
```

Each package has its own `<Version>` in its `.csproj`. Bump only the packages being released; their versions do not need to match. To pack one package, run `dotnet pack <path-to-project.csproj> -c Release -o artifacts/packages`. When changing Core's version, update its PackageReference in both package smoke projects under `tests`.

Publish VersionFlow.Core, VersionFlow.Extensions.DI, VersionFlow.Extensions.Cqrs, VersionFlow.Extensions.Railway, VersionFlow.Extensions.Results, and VersionFlow.Extensions.Results.AspNetCore as needed. The generator project builds but is embedded in Core. CI checks the packed Core using a fresh package-only consumer, including referenced-library discovery.

Run the sample with `dotnet run --project samples/VersionFlow.SampleWeb`. Its README lists the endpoints. Each published project has its own usage README, MIT license, and embedded icon.

MIT. Copyright (c) 2026 Bradley Ables. See LICENSE.
