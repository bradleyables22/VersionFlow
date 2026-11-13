# VersionFlow.Extensions.Cqrs

**1.0.1 · .NET 10+ · MIT**

A dependency-free pair of direct-call handler contracts. No registry, mediator, scanner, or DI dependency is required. Commands and queries use the same contracts; use names and application design to distinguish state changes from reads.

```xml
<PackageReference Include="VersionFlow.Extensions.Cqrs" Version="1.0.1" />
```

## Plain CQRS

```csharp
using VersionFlow.Extensions.Cqrs;

public sealed record GetPrice(int Quantity);
public sealed class GetPriceHandler : ICqrsHandler<GetPrice, decimal>
{
    public Task<decimal> HandleAsync(GetPrice request, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(request.Quantity * 10m);
    }
}
```

Call the concrete handler directly or register it with ordinary DI:

```csharp
services.AddScoped<ICqrsHandler<GetPrice, decimal>, GetPriceHandler>();
```

With the separate DI extension, use `[Service(typeof(ICqrsHandler<GetPrice, decimal>))]` and AddAttributedServices instead. `[Service]` alone registers only the concrete class. Closed generic interfaces are valid for ordinary DI binding.

ICqrsHandler<TRequest> returns Task; ICqrsHandler<TRequest, TResponse> returns Task<TResponse>. Both accept an optional cancellation token. Validation, transactions, authorization, cancellation handling, and exception behavior belong to application code. Native DI requires the exact requested service registration; C# request contravariance does not create extra bindings.

## Versioned CQRS with Core

Install VersionFlow.Core 1.0.1. Its generator requires a non-generic contract, so wrap the CQRS interface:

```csharp
using VersionFlow;
using VersionFlow.Extensions.Cqrs;
using VersionFlow.Extensions.DI;

public interface IGetPriceHandler : ICqrsHandler<GetPrice, decimal> { }

[Service]
[Versioned(typeof(IGetPriceHandler), 1, Key = "Pricing")]
public sealed class GetPriceV1 : IGetPriceHandler
{
    public Task<decimal> HandleAsync(GetPrice request, CancellationToken ct = default)
        => Task.FromResult(request.Quantity * 10m);
}

[Service]
[Versioned(typeof(IGetPriceHandler), 2, Key = "Pricing")]
public sealed class GetPriceV2 : IGetPriceHandler
{
    public Task<decimal> HandleAsync(GetPrice request, CancellationToken ct = default)
        => Task.FromResult(request.Quantity * 9m);
}
```

The Service attribute above comes from the optional VersionFlow.Extensions.DI package. Startup for implementations in the application assembly:

```csharp
builder.Services.AddAttributedServices();
builder.Services.AddVersionFlow(options => options.Classes["Pricing"] = "2");
```

Inject IGetPriceHandler and call HandleAsync normally. Core automatically links generated catalogs and registers the routing interface. Configuration can also come from `AddVersionFlow(builder.Configuration.GetSection("VersionFlow"))`. No catalog or dispatcher call is needed for normal setup.

Do not pass typeof(ICqrsHandler<GetPrice, decimal>) directly to Versioned. Do not use Service interface binding/automatic discovery on the versioned implementations: the dispatcher owns IGetPriceHandler. The inherited HandleAsync method is supported and its configuration key is Pricing.HandleAsync. Directly injecting GetPriceV1 deliberately bypasses routing.

For a multi-project application, reference Core in startup and declaring projects, rebuild them for 1.0.1, and pass the implementation assemblies to AddAttributedServices. Referenced generated catalogs are discovered automatically; DI still needs concrete registrations and constructor dependencies. Core's README covers advanced manual setup and opt-out.

MIT. Copyright (c) 2026 Bradley Ables. Full license included as LICENSE.
