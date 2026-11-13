# VersionFlow.Core

Version **1.0.0** · .NET 10+ · MIT

Configuration-driven service and method version routing with strongly typed generated facades, standard Microsoft DI, immutable configuration snapshots, and fluent version selection.

## Install

```xml
<PackageReference Include="VersionFlow.Core" Version="1.0.0" />
```

The Core package automatically supplies the build-time generator. No separate generator package is required.

## Define and register versions

```csharp
using Microsoft.Extensions.DependencyInjection;
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

For an application assembly named `MyApp`:

```csharp
services.AddScoped<PricingV1>();
services.AddScoped<PricingV2>();
services.AddVersionFlowCatalog<VersionFlow.Generated.Assembly_MyApp.GeneratedCatalog>();
services.AddVersionFlow(options => options.Classes["Pricing"] = "1");
services.AddVersionFlowDispatchers();
```

Inject `IPricing` to receive the scoped facade, or inject `IVersionFlow`:

```csharp
var latest = flow.For<IPricing>().UseLatest().Resolve();
var original = flow.For<IPricing>().UseVersion(1).Resolve();
```

The selector follows explicit choice, method configuration, class configuration, then global default. `Latest` means the highest declared version. Missing versions fail explicitly. Implementations must be registered by concrete type; VersionFlow preserves their normal DI lifetimes and never implicitly activates unregistered services.

## Configuration and methods

```csharp
services.AddVersionFlow(configuration.GetSection("VersionFlow"));
// Alternatively:
services.AddVersionFlowOptions().Bind(configuration.GetSection("VersionFlow")).ValidateOnStart();
```

```json
{
  "VersionFlow": {
    "Default": "Latest",
    "Classes": { "Pricing": "1" },
    "Methods": { "Pricing.Calculate": "2" }
  }
}
```

Method overrides route individual calls to different class versions. Independent operations use `[Versioned(typeof(IContract), Key = "Operations")]` on a registered host and `[Versioned("MethodName", version)]` on its public methods. These two versioning models cannot share a contract/key.

Reload-capable providers update subsequent calls. Invalid changes are logged and rejected while dispatch retains its last valid immutable snapshot. Native options consumers retain native validation behavior. A separate introspection query and later method call are not atomic.

Incompatible signatures can use `flow.For("Orders.Process").Match<TResult>()` or `MatchAsync<TResult>()`, typed `.Version<TService>(number, callback)` branches, and `Execute()`/`ExecuteAsync()`. Declare configurable fluent-only names once with `services.AddVersionFlowOperation("Orders.Process")`; branches declare available versions at execution.

## Boundaries

Link each declaring assembly's generated catalog explicitly before dispatcher registration. Facades and fluent builders must stay within their DI scope. Direct calls to concrete implementations bypass version selection. Generic contracts/methods, overloaded operation names, properties/events, and ref signatures are not supported by the generator in this release. Native AOT is not certified.

The optional `VersionFlow.Extensions.DI` and `VersionFlow.Extensions.Cqrs` packages work independently; Core does not depend on them.

## License

MIT. Copyright (c) 2026 Bradley Ables. The full license is included as `LICENSE`.



Fluent `.For("Key.Method")` names must be declared with `AddVersionFlowOperation` or present in a linked generated catalog. Names are case-sensitive. Unknown names throw `VersionFlowSelectionException` immediately, including when an explicit version is requested; they never silently inherit the global default.

