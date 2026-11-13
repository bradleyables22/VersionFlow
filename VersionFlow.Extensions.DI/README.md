# VersionFlow.Extensions.DI

Version **1.0.0** · .NET 10+ · MIT

Attribute-based registration for Microsoft dependency injection. This package works independently of VersionFlow.Core and the CQRS extension.

```xml
<PackageReference Include="VersionFlow.Extensions.DI" Version="1.0.0" />
```

## Concrete and interface registrations

```csharp
using Microsoft.Extensions.DependencyInjection;
using VersionFlow.Extensions.DI;

[Service] // Concrete type only; scoped by default
public sealed class MyHandler { }

[Service(typeof(IClock), ServiceLifetime.Singleton)]
public sealed class Clock : IClock { }

[Service(ServiceLifetime.Transient, typeof(IWriter), typeof(IExporter))]
public sealed class Writer : IWriter, IExporter { }

services.AddAttributedServices();
```

`[Service]` always registers the concrete class. It does not automatically bind interfaces. Explicit interface/base-type bindings must be assignable from the implementation. The native `ServiceLifetime` enum supports Scoped, Singleton, and Transient. Each binding owns its instance: scoped bindings cache per scope, singleton bindings cache per provider, and transient resolutions create new instances. Interface and concrete bindings are separate, including for scoped and singleton services.

To opt into interface discovery:

```csharp
[Service(ServiceLifetime.Scoped, AsImplementedInterfaces = true)]
public sealed class MyService : IMyService, IAnotherService { }
```

Discovery includes inherited and closed generic interfaces, excludes IDisposable/IAsyncDisposable, and deduplicates explicit bindings.

## Select assemblies

```csharp
services.AddAttributedServices(typeof(MyHandler).Assembly);
services.AddAttributedServicesFromAssemblyContaining<MyHandler>();
```

The parameterless overload scans the immediate calling assembly. Prefer explicit assemblies inside shared startup helpers. Only marked classes are registered, and attributes are not inherited.

Repeated scans are idempotent. Existing identical service-type/implementation-type/lifetime registrations are reused; conflicting registrations are rejected before the collection is changed. Competing implementations for one contract require deliberate native DI registration instead. Constructors are not invoked during scanning.

## Integration and limitations

For VersionFlow class families, use `[Service]` without binding the shared versioned interface: VersionFlow registers that interface's facade separately. CQRS interfaces can be explicitly bound like any other interface; this package does not reference or require CQRS.

Types must be closed concrete classes with public constructors. Register open generics through native DI. Scanning uses reflection and carries trimming warnings. Assembly loader failures are reported rather than silently dropping registrations. Native DI scope validation applies. Every binding is an implementation-type registration; there are no forwarding factories that would cause DI to track the same disposable instance twice. Each instance created by these registrations is disposed once by its owning scope/provider. Use CreateAsyncScope and await using for asynchronous-only disposables. If a class supports both IDisposable and IAsyncDisposable, asynchronous scope/provider disposal calls DisposeAsync, while synchronous disposal calls Dispose.

## Binding identity and ownership

```csharp
[Service(typeof(IWorker))]
public sealed class Worker : IWorker, IDisposable
{
    public void Dispose() { /* release this instance's resources */ }
}
```

This registers the equivalent of:

```csharp
services.AddScoped<Worker>();
services.AddScoped<IWorker, Worker>();
```

Resolving IWorker twice within one scope returns the same instance. Resolving Worker separately creates a different scoped instance. Resolving only IWorker does not create the concrete binding's instance. Both bindings are constructed lazily and independently, and each created instance is disposed exactly once. The same per-binding ownership rule applies to singleton services; transient resolutions always create separate instances.

If two bindings need shared mutable state, inject a separately registered scoped/singleton state service into both. `[Service]` alone continues to register only the concrete type. Arbitrary factory aliases or manually shared disposable objects added outside this scanner still follow the application's own DI ownership choices.
## License

MIT. Copyright (c) 2026 Bradley Ables. The full license is included as `LICENSE`.

