# VersionFlow.Extensions.Cqrs

Version **1.0.0** · .NET 10+ · MIT

A dependency-free pair of typed CQRS handler contracts. There is no registry, mediator, request hierarchy, scanner, or DI dependency. Call handlers directly and use command/query naming to express whether an operation changes state or only reads it.

```xml
<PackageReference Include="VersionFlow.Extensions.Cqrs" Version="1.0.0" />
```

## An operation with a result

```csharp
using VersionFlow.Extensions.Cqrs;

public sealed record CalculateTotal(int Quantity);

public sealed class CalculateTotalHandler : ICqrsHandler<CalculateTotal, decimal>
{
    public Task<decimal> HandleAsync(CalculateTotal request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(request.Quantity * 10m);
    }
}

var handler = new CalculateTotalHandler();
var total = await handler.HandleAsync(new CalculateTotal(3));
```

Operations without a result implement `ICqrsHandler<TRequest>` and return `Task`. Result-bearing operations implement `ICqrsHandler<TRequest, TResponse>` and return `Task<TResponse>`. Both accept an optional cancellation token. Validation, authorization, transactions, cancellation handling, and exception behavior belong to application code.

## Optional dependency injection

Register with native Microsoft DI in the consuming application:

```csharp
services.AddScoped<ICqrsHandler<CalculateTotal, decimal>, CalculateTotalHandler>();
```

Or install the separate `VersionFlow.Extensions.DI` package and annotate the handler with `[Service(typeof(ICqrsHandler<CalculateTotal, decimal>))]`, then call `services.AddAttributedServices()`. Use `[Service]` alone if only concrete injection is wanted. There is no CQRS-specific registration attribute or method.

A handler may implement multiple distinct operation interfaces. The request type is contravariant in C#, but native DI resolves the exact registered service type rather than automatically adding variance-based bindings.

## License

MIT. Copyright (c) 2026 Bradley Ables. The full license is included as `LICENSE`.
