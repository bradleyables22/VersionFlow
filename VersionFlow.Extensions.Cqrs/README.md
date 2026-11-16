# VersionFlow.Extensions.Cqrs

**.NET 10 · MIT**

Dependency-free contracts for calling synchronous and asynchronous handlers directly. No registry, mediator, scanner, or DI dependency is required. Commands and queries use the same contracts; use application names to distinguish state changes from reads.

```sh
dotnet add package VersionFlow.Extensions.Cqrs
```

## Choose a contract

| Work | With a response | Without a response | Method |
| --- | --- | --- | --- |
| Synchronous | `ICqrsHandler<TRequest, TResponse>` | `ICqrsHandler<TRequest>` | `Handle` returns `TResponse` or `void` |
| Asynchronous | `ICqrsAsyncHandler<TRequest, TResponse>` | `ICqrsAsyncHandler<TRequest>` | `HandleAsync` returns `Task<TResponse>` or `Task` |

All four methods accept an optional `CancellationToken`. The implementation decides how to observe cancellation. The contracts do not schedule background work, catch exceptions, validate requests, or wrap one calling convention in the other.

Use synchronous handlers for synchronous work and asynchronous handlers for asynchronous I/O. Call `Handle` directly; await `HandleAsync`.

## Synchronous handler

```csharp
using VersionFlow.Extensions.Cqrs;

public sealed record GetPrice(int Quantity);

public sealed class GetPriceHandler : ICqrsHandler<GetPrice, decimal>
{
    public decimal Handle(GetPrice request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return request.Quantity * 10m;
    }
}
```

Call it directly without DI:

```csharp
ICqrsHandler<GetPrice, decimal> pricing = new GetPriceHandler();
decimal total = pricing.Handle(new GetPrice(3)); // 30
```

For a command with no response, implement `ICqrsHandler<TRequest>` with `void Handle(TRequest request, CancellationToken cancellationToken = default)`.

## Asynchronous handler

This example uses an application-defined data source:

```csharp
using VersionFlow.Extensions.Cqrs;

public sealed record GetStoredPrice(string ProductId);

public interface IPriceStore
{
    Task<decimal> ReadAsync(string productId, CancellationToken cancellationToken);
}

public sealed class GetStoredPriceHandler(IPriceStore store)
    : ICqrsAsyncHandler<GetStoredPrice, decimal>
{
    public Task<decimal> HandleAsync(
        GetStoredPrice request, CancellationToken cancellationToken = default)
        => store.ReadAsync(request.ProductId, cancellationToken);
}
```

Inject `ICqrsAsyncHandler<GetStoredPrice, decimal>` and await its `HandleAsync` method. For an asynchronous command without a response, implement `ICqrsAsyncHandler<TRequest>` and return `Task`.

## Dependency injection

With Microsoft DI, register the exact interface callers request:

```csharp
services.AddScoped<ICqrsHandler<GetPrice, decimal>, GetPriceHandler>();
services.AddScoped<ICqrsAsyncHandler<GetStoredPrice, decimal>, GetStoredPriceHandler>();
// Also register your IPriceStore implementation.
```

With the optional `VersionFlow.Extensions.DI` package, use `[Service(typeof(ICqrsHandler<GetPrice, decimal>))]` or the corresponding async interface, then call `AddAttributedServices()`. `[Service]` alone registers only the concrete class.

Closed generic interfaces are valid for ordinary DI binding. Request contravariance does not make native DI create extra bindings. Validation, transactions, authorization, cancellation, and exception handling belong to application code.

## Versioned handlers with Core

Core routes DI-managed services and requires a non-generic contract. Wrap either handler interface in a named interface, register concrete implementations, then call `AddVersionFlow`:

```csharp
using VersionFlow;
using VersionFlow.Extensions.Cqrs;

public interface IGetPriceHandler : ICqrsHandler<GetPrice, decimal> { }

[Versioned(typeof(IGetPriceHandler), 1, Key = "Pricing")]
public sealed class GetPriceV1 : IGetPriceHandler
{
    public decimal Handle(GetPrice request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return request.Quantity * 10m;
    }
}

[Versioned(typeof(IGetPriceHandler), 2, Key = "Pricing")]
public sealed class GetPriceV2 : IGetPriceHandler
{
    public decimal Handle(GetPrice request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return request.Quantity * 9m;
    }
}
```

Startup for implementations in the application assembly:

```csharp
builder.Services.AddScoped<GetPriceV1>();
builder.Services.AddScoped<GetPriceV2>();
builder.Services.AddVersionFlow(options =>
    options.Overrides.Classes["Pricing"] = new() { Version = "2" });
```

Inject `IGetPriceHandler` and call `Handle`. Core registers the routing interface; a quantity of 3 returns 27. Configuration binding also works with `AddVersionFlow(builder.Configuration.GetSection("VersionFlow"))`.

For asynchronous routing, inherit `ICqrsAsyncHandler<TRequest, TResponse>` and implement `HandleAsync` instead. Both no-response forms work too. The method configuration key is `Pricing.Handle` for the synchronous example and `Pricing.HandleAsync` for an async wrapper. Defaults, `Latest`, explicit choices, and configuration reloads work with both.

Do not pass a generic handler interface directly to `[Versioned]`. With attribute DI, use `[Service]` without interface bindings on versioned implementations; the generated dispatcher owns the wrapper interface. Direct calls to concrete handlers bypass routing.

For multiple projects, install Core in startup and declaring projects and register every concrete version and its dependencies. See the [Core README](../VersionFlow.Core/README.md) for discovery and scope guidance.

## Migrating existing async handlers

The old `ICqrsHandler` interfaces exposed `HandleAsync`. Those async contracts are now named `ICqrsAsyncHandler`:

- Replace `ICqrsHandler<TRequest, TResponse>` with `ICqrsAsyncHandler<TRequest, TResponse>` on existing async implementations, consumers, DI registrations, and wrapper base interfaces.
- Replace the one-type-argument async contract with `ICqrsAsyncHandler<TRequest>`.
- Keep existing `HandleAsync` implementations, awaits, and `HandleAsync` configuration keys.

This is a breaking API change: rebuild consumers against the updated package. The `ICqrsHandler` name now means synchronous `Handle`; there is no automatic sync/async adapter. If deliberately converting an operation to synchronous work, update its method override key from `HandleAsync` to `Handle` too.

MIT. Copyright (c) 2026 Bradley Ables. Full license included as LICENSE.
