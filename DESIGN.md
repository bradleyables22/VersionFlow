# Design notes

## Scope and packaging

The user's requested project layout takes precedence over the proposed multi-package runtime split: attributes, runtime, options, and optional DI extensions live in `VersionFlow.Core`. The compiler-hosted generator stays in a separate non-packable development project; its DLL is bundled in the Core NuGet package under analyzers/dotnet/cs. Runtime projects target .NET 10, the generator targets .NET Standard 2.0, and all three published packages use `1.0.0`. Core has a build-only project reference to the generator, with no generator or Roslyn runtime dependency. Source-tree consumers still reference the generator project as an analyzer explicitly; package consumers need only Core. Unit, compiler, and integration tests share one test project; a separate fixture assembly verifies catalog linking.

## Catalog linking

A generated catalog contains typed facade factories, concrete implementation types, stable logical keys, and per-method version sets. Linking is explicit once per declaring assembly, rather than relying on module initializer order or reflection scanning. No per-version runtime registration or invocation table is maintained by application authors. The catalog is immutable after DI construction. Add all catalogs before dispatcher registration and do not mutate service registrations after building a provider.

## DI boundaries

Facades and the central fluent runtime are scoped. The selector and immutable configuration store are singleton services containing no implementation instances. Each invocation resolves its selected implementation from the originating scope through the existing provider. Registered implementation lifetimes are untouched, including disposal. Registration checks use `IServiceProviderIsService` when available, without activation. Exceptions from user constructors and callbacks propagate unchanged.

The optional adapter rejects existing unkeyed contract registrations. Repeated adapter calls recognize their own registrations. Host startup checks later conflicts and missing concrete registrations; standard `ValidateScopes` remains responsible for lifetime graph validation. Containers lacking `IServiceProviderIsService` receive a clear registration error if resolution returns null, while their own thrown exceptions propagate.

## Reload publication

`SnapshotStore` uses the standard options factory, including configure, postconfigure, and validation registrations, and subscribes to `IOptionsChangeTokenSource` tokens. This is intentional: native `IOptionsMonitor` validates before invoking subscribers, so an invalid update would never reach an `OnChange` rejection handler. Candidates are serialized under a lock and published with volatile reads/writes. A selection captures exactly one immutable snapshot. Invalid candidates are logged and discarded. Native options consumers retain native validation semantics; VersionFlow's last-good dispatch state is separate.

Startup's lightweight options configuration pass reads the `ValidateOnStart` flag without invoking validators. If enabled, the normal factory validates and the store is initialized. With the flag disabled, validation occurs when the runtime is first used. An explicit Microsoft options-builder `ValidateOnStart()` remains authoritative. Configure delegates should follow normal options guidance and be deterministic and side-effect free.

## Fluent operation names

Compatible operations have generated metadata; dynamic callback branches do not exist at startup. `AddVersionFlowOperation("Orders.Process")` declares a valid configuration name once. It does not declare branch versions or register implementations. This small addition reconciles strict typo detection with dynamically constructed fluent callbacks. Availability is validated when executing those callbacks.

## API choices

- `Resolve()` always returns a facade, including when all methods currently select the same class. This preserves subsequent method overrides and reloads.
- Operations-host class-level introspection throws and requests a method, rather than inventing a single version for methods with different version sets.
- `Execute`/`ExecuteAsync` select once then invoke exactly one callback. Separate introspection and later invocation are not atomic. Captured-selection replay is not provided in this release.
- Matching uses immutable concrete builders, with `IVersionFlowOperation` also exposed. Async `Version` has tokenless and token-aware Task overloads. ValueTask adaptation is explicit.
- Strict unique operation names avoid ambiguous configuration keys for overloads. Property/event/generic support is deferred with compiler diagnostics rather than silently incomplete facades.
- AOT certification, automatic cross-assembly discovery, implicit activation, and matrices of class/method versions are deferred.

## Generator diagnostics

| ID | Meaning |
| --- | --- |
| VF001 | Invalid contract or missing/incompatible interface implementation |
| VF002 | Duplicate class or operation version |
| VF003 | Non-positive version |
| VF004 | Blank, duplicate, or conflicting logical key |
| VF005 | Missing or unknown operation |
| VF006 | Incompatible signature or overloaded operation name |
| VF007 | Unsupported generic method/constraints |
| VF008 | Unsupported ref/out/in or ref-return signature |
| VF009 | Unsupported interface member or pointer signature |
| VF010 | Conflicting version models or method annotation without an operations host |
| VF011 | Inaccessible, generic, abstract, static, or non-instantiable implementation |
| VF012 | Versioned overload is invalid for its target, or method specifies Key |

The incremental syntax pipeline uses Roslyn semantic symbols, deterministic ordinal ordering, escaped string literals, and fully qualified nullable-aware types. Generated class-family calls cast concrete resolutions to the declaring interface, supporting explicit implementations. Independent operation calls target annotated host methods directly.

## References

- [Microsoft options infrastructure](https://learn.microsoft.com/en-us/dotnet/core/extensions/options)
- [Roslyn incremental generators](https://github.com/dotnet/roslyn/blob/main/docs/features/incremental-generators.md)

## Optional extensions

`VersionFlow.Extensions.Cqrs` contains only the two direct-call handler contracts ported from MicroCqrs. It has no DI dependency, registration attributes, registry, scanner, or runtime dispatch system. Native DI, the attribute extension, or direct construction can be used independently.

`VersionFlow.Extensions.DI` registers classes marked by `[Service]`, with native `ServiceLifetime`, explicit interface/base-type bindings, and optional `AsImplementedInterfaces` discovery. Interface-first shorthand defaults to scoped. Discovered contracts include inherited and closed generic interfaces but exclude disposal interfaces; explicit and discovered bindings are deduplicated. General DI attributes can bind CQRS interfaces without any knowledge of CQRS in the registration system. Core remains independent of both extensions.

Assembly scans are explicit and startup-only, marked `RequiresUnreferencedCode`; this does not change the core generator's scan-free catalog linking. The parameterless entry point disables inlining so `Assembly.GetCallingAssembly()` identifies the immediate caller consistently. Scans stage registrations before committing, reject incomplete assembly loading and invalid contracts, preserve existing identical service-type/implementation-type/lifetime registrations, and detect registration conflicts. Concrete, interface, and base-type bindings use native implementation-type descriptors. Every binding has an independent lifetime cache and instance. This intentionally replaces the pre-publication factory-alias design: Microsoft DI captures disposable factory results for each binding, which could dispose one aliased object multiple times. Native type descriptors give each created object one owner without custom activation, proxies, reflection into DI internals, or a replacement service provider. Only resolved bindings construct objects; singleton instances live until provider disposal, scoped instances until their scope ends, and transient objects are individually tracked. Async-only objects require asynchronous disposal; dual-interface objects use DisposeAsync during asynchronous disposal. Shared state belongs in a separately registered dependency, not in assumptions that different service types resolve to the same implementation instance.


Disposal regression tests cover all three lifetimes, explicit and discovered interfaces, IDisposable-only/IAsyncDisposable-only/dual-interface implementations, repeated resolution, IEnumerable resolution, repeated scope/provider disposal, lazy interface-only construction, and native registration reuse.


Core uses one VersionedAttribute with three constructor overloads: (Type contractType, int version) for a class version, (Type contractType) for an operations host, and (string operation, int version) for a method. The generator validates the constructor symbol against the declaration target and rejects mixed models. This replaces the three longer attribute names before the first publication; no legacy aliases are shipped.



## Package release check

CI packs the solution, then restores and runs the package-consumer project directly with dotnet commands. The standalone `tests/VersionFlow.PackageSmoke` project references only the Core package and verifies generation, class/method routing, explicit choices, fluent execution, and rejection of unknown operation names. An isolated CI package cache, exclusive local VersionFlow source mapping in the consumer NuGet.Config, and a clean checkout prevent cached packages or generated files from hiding packaging failures. The consumer is intentionally outside the solution so normal restore does not require a previously packed release.

