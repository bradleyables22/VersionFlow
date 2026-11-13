# VersionFlow.Core

**1.0.1 · .NET 10+ · MIT**

Strongly typed class and method version routing. The package includes its source generator; install only Core for versioning.

```xml
<PackageReference Include="VersionFlow.Core" Version="1.0.1" />
```

## Quick start

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

Register concrete implementations with Microsoft DI, then configure VersionFlow:

```csharp
builder.Services.AddScoped<PricingV1>();
builder.Services.AddScoped<PricingV2>();
builder.Services.AddVersionFlow(builder.Configuration.GetSection("VersionFlow"));
```

```json
{
  "VersionFlow": {
    "Default": "Latest",
    "Classes": { "Pricing": "2" }
  }
}
```

Inject IPricing and call Calculate normally. VersionFlow automatically discovers generated catalogs and registers the interface dispatcher; the configured implementation receives the call. There are no per-version VersionFlow registration calls.

With the optional VersionFlow.Extensions.DI package, annotate each implementation with `[Service]` and replace the concrete registrations with `builder.Services.AddAttributedServices()`. That makes startup two calls: AddAttributedServices, then AddVersionFlow. Register constructor dependencies normally too. Do not use Service interface bindings on versioned implementations: the generated dispatcher owns IPricing.

## Attribute forms and selection

| Declaration | Meaning |
| --- | --- |
| `[Versioned(typeof(IContract), 1, Key = "Service")]` | Class version |
| `[Versioned(typeof(IContract), Key = "Operations")]` | Host of independently versioned methods |
| `[Versioned(nameof(IContract.Method), 2)]` | Method version on that host |

Versions are positive integers. Keys are case-sensitive; an omitted class key uses the fully qualified contract name. Precedence is explicit choice, method configuration, class configuration, then Default (Latest by default). Latest selects the highest version available for that operation. Unavailable versions throw.

`Methods["Pricing.Calculate"] = "1"` overrides the Pricing class choice for Calculate only. Independent method hosts use the same selection rules but have separate available versions per method. Class-family and independent-method models cannot share a contract/key.

## Explicit selection and fluent branches

Inject IVersionFlow:

```csharp
var selected = flow.Select("Pricing", "Calculate");
var original = flow.For<IPricing>().UseVersion(1).Resolve();
var newest = flow.For<IPricing>().UseLatest().Resolve();
```

Resolve always returns a generated facade. Explicit choices override all its method settings. Introspection returns the selected number, source, available versions, and snapshot identity. Operations-host introspection requires a method.

For incompatible signatures, declare a fluent-only operation name with `services.AddVersionFlowOperation("Orders.Process")` and register the callback service types in DI:

```csharp
var result = await flow.For("Orders.Process").MatchAsync<OrderResult>()
    .Version<OrderV1>(1, service => service.Process(orderId))
    .Version<OrderV2>(2, (service, ct) => service.Process(orderId, true, ct))
    .ExecuteAsync(cancellationToken);
```

Only the selected branch resolves/executes. Every callback returns Task<OrderResult>. Match<TResult>/Execute provide synchronous branching. UseVersion/UseLatest before matching overrides configuration. Unknown operation names throw at For; generated operation names already exist and need no AddVersionFlowOperation call. Branch versions are checked at execution.

## CQRS contracts

The generic CQRS interface is valid for ordinary DI but cannot be the Versioned contract. Wrap it:

```csharp
public interface IGetPriceHandler : ICqrsHandler<GetPrice, decimal> { }

[Versioned(typeof(IGetPriceHandler), 1, Key = "Pricing")]
public sealed class GetPriceV1 : IGetPriceHandler
{
    public Task<decimal> HandleAsync(GetPrice request, CancellationToken ct = default)
        => Task.FromResult(request.Quantity * 10m);
}
```

GetPrice is your request type. Install VersionFlow.Extensions.Cqrs and import its namespace for ICqrsHandler. Register the concrete versions and call AddVersionFlow normally. Inject IGetPriceHandler, not the generic base interface. Its inherited HandleAsync method is supported; the method key is Pricing.HandleAsync.

## Options and lifetime behavior

Choose configuration binding, `AddVersionFlow(options => ...)`, `AddVersionFlowOptions().Bind(...).ValidateOnStart()`, or parameterless AddVersionFlow for Latest defaults. All automatically wire catalogs and dispatchers. Additional Configure/PostConfigure/validators participate normally.

Facades and IVersionFlow are scoped. Enable native DI scope validation and do not retain them beyond their scope. Existing implementation lifetimes are preserved. Direct concrete injection bypasses selection. Missing concrete registrations fail rather than causing implicit activation.

Host startup validates by default. Set ValidateOnStart=false to defer VersionFlow validation; explicit options-builder ValidateOnStart still applies. Reload-capable providers affect subsequent calls. Invalid updates retain the last valid dispatch snapshot and are logged; native options consumers retain native validation semantics. Each call uses one immutable snapshot, but a separate selection query and later call are not atomic. State is not transferred between versions.

## Referenced projects and advanced setup

Install Core in the startup project and declaring libraries, then rebuild them for 1.0.1. The generator emits assembly metadata and links to referenced generated catalogs. AddVersionFlow discovers from the immediate calling assembly and follows those links, without scanning arbitrary loaded assemblies. Register the concrete implementations from every discovered library; when using attribute DI, pass those assemblies to AddAttributedServices.

For shared bootstrap helpers use `AddVersionFlow(configurationSection, assembly: typeof(MyHandler).Assembly)` to select a different discovery root. Use the application assembly when it combines multiple libraries.

Normal applications no longer need AddVersionFlowCatalog or AddVersionFlowDispatchers. For a deliberate manual/fluent-only integration:

```csharp
services.AddVersionFlowCatalog<VersionFlow.Generated.Assembly_MyApp.GeneratedCatalog>();
services.AddVersionFlow(configuration.GetSection("VersionFlow"), autoRegister: false);
// Only if normal interface injection should use routing:
services.AddVersionFlowDispatchers();
```

Explicit catalogs registered before automatic setup define its catalog scope; referenced catalogs are then your responsibility. AutoRegister=false disables both discovery and dispatcher registration while retaining the runtime/options. Existing interface registrations are never silently replaced. Host startup checks later conflicts when startup validation is enabled.

## Limitations

Versioned contracts must be non-generic. Inherited methods from closed generic interfaces are supported through a non-generic wrapper. Generic methods/implementation types, overloaded operation names, properties/events/indexers, ref signatures, static/default interface methods, and pointer signatures are unsupported. Attributes do not intercept direct calls. Dispatch uses generated typed calls; Native AOT is not certified.

MIT. Copyright (c) 2026 Bradley Ables. Full license included as LICENSE.
