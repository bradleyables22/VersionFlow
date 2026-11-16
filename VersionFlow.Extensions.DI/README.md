# VersionFlow.Extensions.DI

**.NET · MIT**

Attribute-based registration for Microsoft dependency injection, independent of Core and CQRS.

```sh
dotnet add package VersionFlow.Extensions.DI
```

## Register services

```csharp
using Microsoft.Extensions.DependencyInjection;
using VersionFlow.Extensions.DI;

[Service] // Concrete only, scoped by default
public sealed class MyHandler { }

[Service(typeof(IClock), ServiceLifetime.Singleton)]
public sealed class Clock : IClock { }

[Service(ServiceLifetime.Transient, typeof(IWriter), typeof(IExporter))]
public sealed class Writer : IWriter, IExporter { }

services.AddAttributedServices();
```

Interfaces in these examples are your application contracts. Every marked class is registered by concrete type; optional interface/base-type bindings must be assignable from it. All native ServiceLifetime values are supported: Scoped, Singleton, and Transient.

To opt into all implemented interfaces:

```csharp
[Service(ServiceLifetime.Scoped, AsImplementedInterfaces = true)]
public sealed class MyService : IMyService, IAnotherService { }
```

Discovery includes inherited and closed generic interfaces, excludes IDisposable/IAsyncDisposable, and deduplicates explicit bindings. It defaults to false. Attributes are not inherited by subclasses.

## Choose assemblies

Parameterless AddAttributedServices scans the immediate calling assembly. For libraries or shared startup helpers:

```csharp
services.AddAttributedServices(typeof(MyHandler).Assembly, typeof(OtherHandler).Assembly);
services.AddAttributedServicesFromAssemblyContaining<MyHandler>();
```

Repeated scans are idempotent. Identical service-type/implementation-type/lifetime registrations are reused. Conflicts are rejected before appending registrations; existing services are never replaced silently. Deliberate multiple implementations of one service contract require native DI registration. Scanning never invokes service constructors. Constructor dependencies must also be registered.

## Binding identity and disposal

`[Service(typeof(IWorker))]` on Worker is equivalent to:

```csharp
services.AddScoped<Worker>();
services.AddScoped<IWorker, Worker>();
```

Worker and IWorker have separate scoped instances. Resolving IWorker repeatedly in one scope returns its same instance; singleton bindings cache per provider and transient resolutions create new instances. Only resolved bindings construct objects. For shared state, inject a separately registered shared dependency.

Every binding uses an implementation-type registration, not a forwarding factory, so each created disposable instance has one DI owner and is disposed once. Async-only implementations require CreateAsyncScope/await using. Async disposal prefers DisposeAsync when both disposal interfaces are implemented. Manually added factories/shared objects remain subject to your own ownership choices.

## With VersionFlow.Core

Annotate versioned classes with concrete-only Service plus Versioned:

```csharp
[Service]
[Versioned(typeof(IPricing), 1, Key = "Pricing")]
public sealed class PricingV1 : IPricing { /* implementation */ }
```

Install Core and import VersionFlow for Versioned. Startup:

```csharp
builder.Services.AddAttributedServices();
builder.Services.AddVersionFlow(builder.Configuration.GetSection("VersionFlow"));
```

AddVersionFlow automatically discovers generated catalogs and registers scoped interface dispatchers. Do not bind IPricing yourself using Service interface bindings; it belongs to the dispatcher. Include implementation assemblies in the DI scan for multi-project applications. Core's automatic catalog discovery does not register concrete types for you.

For plain CQRS handlers, explicit closed generic interface binding is valid. For versioned CQRS, define a non-generic wrapper inheriting `ICqrsHandler<TRequest, TResult>` for synchronous work or `ICqrsAsyncHandler<TRequest, TResult>` for asynchronous work, use it as the Versioned contract, and keep Service concrete-only. See the Core and CQRS READMEs for full examples.

## Limitations

Types must be closed concrete classes with public constructors. Register open generics through native DI. Scanning uses reflection and carries trimming warnings; loader failures are reported rather than silently dropping services. Enable native DI scope validation. This package does not provide a container or runtime proxy system.

MIT. Copyright (c) 2026 Bradley Ables. Full license included as LICENSE.
