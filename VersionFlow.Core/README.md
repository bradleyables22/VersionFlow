# VersionFlow.Core

**.NET 10 · MIT**

Choose which implementation receives a call through configuration while consumers use an ordinary typed interface. DI creates your implementations; generated dispatchers route each call to the selected version.

## Install

Target .NET 10 or later. Install Core in the startup project and every library declaring versioned implementations. The package includes its source generator; no separate generator package is needed.

```sh
dotnet add package VersionFlow.Core
```

## Quick start

This example uses ASP.NET Core and standard Microsoft DI. No extension packages are required.

### 1. Define the contract and implementations

Add these types to `Pricing.cs`:

```csharp
using VersionFlow;

public interface IPricing { decimal Calculate(int quantity); }

[Versioned(typeof(IPricing), 1, Key = "Pricing")]
public sealed class PricingV1 : IPricing
{
    public decimal Calculate(int quantity) => quantity * 10m;
}

[Versioned(typeof(IPricing), 2, Key = "Pricing")]
public sealed class PricingV2 : IPricing
{
    public decimal Calculate(int quantity) => quantity * 9m;
}
```

### 2. Register and call the service

In `Program.cs`:

```csharp
using VersionFlow;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddScoped<PricingV1>();
builder.Services.AddScoped<PricingV2>();
builder.Services.AddVersionFlow(builder.Configuration.GetSection("VersionFlow"));

var app = builder.Build();
app.MapGet("/price/{quantity:int}", (int quantity, IPricing pricing) =>
    pricing.Calculate(quantity));
app.Run();
```

### 3. Choose a version

Add this section to `appsettings.json`:

```json
{
  "VersionFlow": {
    "Default": "Latest",
    "Overrides": { "Classes": { "Pricing": { "Version": "2" } } }
  }
}
```

Run the application and request `/price/10`: version 2 returns `90`. Change `Version` to `"1"` and the same request returns `100` once configuration reloads. Without an override, `Latest` selects version 2.

Inject `IPricing` and call `Calculate` normally. VersionFlow automatically discovers generated catalogs and registers the interface dispatcher; the configured implementation receives the call. There are no per-version VersionFlow registration calls.

With the optional VersionFlow.Extensions.DI package, annotate each implementation with `[Service]` and replace the concrete registrations with `builder.Services.AddAttributedServices()`. That makes startup two calls: AddAttributedServices, then AddVersionFlow. Register constructor dependencies normally too. Do not use Service interface bindings on versioned implementations: the generated dispatcher owns IPricing.

## Attribute forms and selection

| Declaration | Meaning |
| --- | --- |
| `[Versioned(typeof(IContract), 1, Key = "Service")]` | Class version |
| `[Versioned(typeof(IContract), Key = "Operations")]` | Host of independently versioned methods |
| `[Versioned(nameof(IContract.Method), 2)]` | Method version on that host |

Versions are positive integers. Keys are case-sensitive; an omitted class key uses the fully qualified contract name. Precedence is explicit choice, method configuration, class configuration, then Default (Latest by default). Latest selects the highest version available for that operation. Unavailable versions throw.

Overrides are nested by class, with short method names inside each class:

```json
{
  "VersionFlow": {
    "Default": "Latest",
    "Overrides": {
      "Classes": {
        "Pricing": {
          "Version": "1",
          "Methods": { "Calculate": "2" }
        }
      }
    }
  }
}
```

`Version` is optional: omitted class versions inherit `Default`, while method overrides still take priority. In code, use `options.Overrides.Classes["Pricing"] = new() { Version = "1", Methods = { ["Calculate"] = "2" } };`. Independent method hosts use the same selection rules but have separate available versions per method. Class-family and independent-method models cannot share a contract/key.

Fluent-only `Orders.Process` uses `Overrides.Classes.Orders.Methods.Process`; a qualified group such as `Company.Orders` remains the full class dictionary key. This replaces the previous top-level `Classes` and `Methods` configuration and options properties; migrate existing configuration to the nested structure.

## Independently versioned methods

Use an operations host when individual methods need their own version sets. The host does not implement the contract; annotated methods supply its implementations:

```csharp
using VersionFlow;

public interface IPricingOperations
{
    decimal Calculate(int quantity);
}

[Versioned(typeof(IPricingOperations), Key = "PricingOps")]
public sealed class PricingOperations
{
    [Versioned(nameof(IPricingOperations.Calculate), 1)]
    public decimal Original(int quantity) => quantity * 10m;

    [Versioned(nameof(IPricingOperations.Calculate), 3)]
    public decimal Newest(int quantity) => quantity * 9m;
}
```

Register `services.AddScoped<PricingOperations>()` before `AddVersionFlow`, then inject `IPricingOperations`. `Latest` selects version 3. To select version 1, set `Overrides.Classes.PricingOps.Methods.Calculate` to `"1"`.

## Inspect or explicitly select a version

Inject IVersionFlow:

```csharp
var selected = flow.Select("Pricing", "Calculate");
var original = flow.For<IPricing>().UseVersion(1).Resolve();
var newest = flow.For<IPricing>().UseLatest().Resolve();
```

Resolve always returns a generated facade. Explicit choices override all its method settings. Introspection returns the selected number, source, available versions, and snapshot identity. Operations-host introspection requires a method.

## Fluent branches for incompatible signatures

For incompatible signatures, declare a fluent-only operation name with `services.AddVersionFlowOperation("Orders.Process")` and register the callback service types in DI:

```csharp
var result = await flow.For("Orders.Process").MatchAsync<OrderResult>()
    .Version<OrderV1>(1, service => service.Process(orderId))
    .Version<OrderV2>(2, (service, ct) => service.Process(orderId, true, ct))
    .ExecuteAsync(cancellationToken);
```

Only the selected branch resolves/executes. In this example, `OrderV1`, `OrderV2`, `OrderResult`, and `orderId` are application-defined. Every callback returns `Task<OrderResult>`; adapt differing return types inside the callback. Callback exceptions propagate; cancellation is passed to token-aware callbacks. Match<TResult>/Execute provide synchronous branching. UseVersion/UseLatest before matching overrides configuration. Unknown operation names throw at For; generated operation names already exist and need no AddVersionFlowOperation call. Branch versions are checked at execution.

## CQRS contracts

The generic CQRS interface is valid for ordinary DI but cannot be the Versioned contract. Wrap it:

```csharp
using VersionFlow;
using VersionFlow.Extensions.Cqrs;

public sealed record GetPrice(int Quantity);
public interface IGetPriceHandler : ICqrsHandler<GetPrice, decimal> { }

[Versioned(typeof(IGetPriceHandler), 1, Key = "Pricing")]
public sealed class GetPriceV1 : IGetPriceHandler
{
    public Task<decimal> HandleAsync(GetPrice request, CancellationToken ct = default)
        => Task.FromResult(request.Quantity * 10m);
}
```

Install `VersionFlow.Extensions.Cqrs` for `ICqrsHandler`. Register the concrete versions and call AddVersionFlow normally. Inject IGetPriceHandler, not the generic base interface. Its inherited HandleAsync method is supported; the method key is Pricing.HandleAsync.

## Feature flags

Use `VersionFlow:Flags` for named boolean gates independent of version selection:

```json
{
  "VersionFlow": {
    "Flags": { "EnableExports": true, "NewCheckout": false }
  }
}
```

```csharp
if (flow.IsEnabled("EnableExports"))
{
    // Export functionality
}
```

`flow` is the existing scoped `IVersionFlow`. For code configuration, set `options.Flags["EnableExports"] = true`. Names are case-sensitive and missing flags return false. No versioned service is required. Valid configuration reloads affect subsequent checks even in an existing scope; invalid updates retain the entire last valid snapshot, including flags and version settings. Flags never alter version selection.

## Options and lifetime behavior

Choose configuration binding, `AddVersionFlow(options => ...)`, `AddVersionFlowOptions().Bind(...).ValidateOnStart()`, or parameterless AddVersionFlow for Latest defaults. All automatically wire catalogs and dispatchers. Additional Configure/PostConfigure/validators participate normally.

Facades and IVersionFlow are scoped. Enable native DI scope validation and do not retain them beyond their scope. Existing implementation lifetimes are preserved. Direct concrete injection bypasses selection. Register all discovered implementations, including inactive versions, and their constructor dependencies. Missing concrete registrations fail rather than causing implicit activation.

Host startup validates by default. Set ValidateOnStart=false to defer VersionFlow validation; explicit options-builder ValidateOnStart still applies. Reload-capable providers affect subsequent calls. Invalid updates retain the last valid dispatch snapshot and are logged; native options consumers retain native validation semantics. Each call uses one immutable snapshot, but a separate selection query and later call are not atomic. State is not transferred between versions.

## Referenced projects

Install Core in the startup project and declaring libraries, then rebuild them. The generator emits assembly metadata and links to referenced generated catalogs. AddVersionFlow discovers from the immediate calling assembly and follows those links, without scanning arbitrary loaded assemblies. Register the concrete implementations from every discovered library; when using attribute DI, pass those assemblies to AddAttributedServices.

For shared bootstrap helpers use `AddVersionFlow(configurationSection, assembly: typeof(MyHandler).Assembly)` to select a different discovery root. Use the application assembly when it combines multiple libraries.

## Advanced manual setup

Normal applications do not need `AddVersionFlowCatalog` or `AddVersionFlowDispatchers`. For a deliberate manual/fluent-only integration:

```csharp
services.AddVersionFlowCatalog<VersionFlow.Generated.Assembly_MyApp.GeneratedCatalog>();
services.AddVersionFlow(configuration.GetSection("VersionFlow"), autoRegister: false);
// Only if normal interface injection should use routing:
services.AddVersionFlowDispatchers();
```

Explicit catalogs registered before automatic setup define its catalog scope; referenced catalogs are then your responsibility. `autoRegister: false` disables both discovery and dispatcher registration while retaining the runtime/options. Existing interface registrations are never silently replaced. Host startup checks later conflicts when startup validation is enabled.

## Limitations

Versioned contracts must be non-generic. Inherited methods from closed generic interfaces are supported through a non-generic wrapper. Generic methods/implementation types, overloaded operation names, properties/events/indexers, ref signatures, static/default interface methods, and pointer signatures are unsupported. Attributes do not intercept direct calls. Dispatch uses generated typed calls; Native AOT is not certified.

## Troubleshooting

| Symptom | Check |
| --- | --- |
| No generated contract or catalog is found | Install Core in the startup project and declaring libraries, rebuild, and check the discovery root when using a shared startup helper. |
| An implementation is missing | Register every version by concrete type and register its constructor dependencies. |
| An interface registration conflicts | Let VersionFlow register the dispatcher. With attribute DI, use `[Service]` without interface bindings. |
| Configuration is rejected | Check case-sensitive keys, short method names under `Methods`, and available version numbers. Missing versions never silently fall back. |
| A declaration fails to compile | Check the limitations above and the generator diagnostic reported by the compiler. |

MIT. Copyright (c) 2026 Bradley Ables. Full license included as LICENSE.
