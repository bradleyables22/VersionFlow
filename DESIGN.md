# Design notes — 1.0.1

## Packages

Core contains attributes, selection/options runtime, fluent APIs, and dispatcher DI integration. Its NuGet package bundles the incremental generator under analyzers/dotnet/cs. The generator stays a separate non-packable netstandard2.0 development project. Runtime packages target net10.0. DI and CQRS extensions are independently usable; Core does not depend on either.

## Automatic startup

AddVersionFlow and AddVersionFlowOptions install runtime services, discover generated catalogs, and register scoped interface dispatchers by default. Each overload captures its immediate calling assembly with inlining disabled; the optional assembly argument selects another root for shared helpers. Original 1.0.0 overload signatures are retained for already-compiled callers.

The generator emits GeneratedVersionFlowCatalogAttribute assembly metadata identifying a public catalog type, plus typed links to catalogs on referenced assemblies. Startup reads that one metadata attribute, constructs catalog metadata, and traverses/deduplicates the graph by catalog type. It does not scan arbitrary loaded assemblies, inspect service constructors for activation, or construct application implementations. Referenced catalog collections are evaluated lazily during traversal to avoid eagerly expanding diamond graphs.

Automatic discovery state belongs to the service collection, not a process-global registry. Repeated automatic calls can add additional explicit discovery roots and do not duplicate catalogs/dispatchers. Explicit catalogs registered before automatic setup intentionally define a manual discovery scope for compatibility. AutoRegister=false disables discovery and dispatcher registration while retaining the runtime/options; the manual catalog/dispatcher APIs remain available.

All versioned implementation assemblies and the startup project should reference Core and be rebuilt with 1.0.1 metadata. Referenced implementations still need concrete DI registrations. Libraries built with the old generator require explicit catalog linking or rebuilding. Native AOT is not certified; startup uses reflection for generated assembly metadata, and the DI extension scans attributed types.

## DI and ownership

Facades and IVersionFlow are scoped. The selector/configuration store are singletons holding no implementation instances. Each call resolves the selected implementation through its originating scope using existing DI registrations. Lifetimes and disposal remain native. Missing concrete registrations and conflicting unkeyed interfaces fail explicitly. Existing interfaces are never silently replaced; enabled host startup validation detects registrations added after automatic dispatchers.

The separate DI extension emits native implementation-type descriptors. Concrete and interface bindings each have their own lifetime cache and instance, preventing repeated disposal of a shared factory-aliased instance. State sharing belongs in a separately registered dependency. Async-only implementations require asynchronous disposal. Repeated scans reuse identical registrations, while conflicts are validated before changes are appended.

## Configuration

Precedence is explicit choice, method, class, then global default. Available versions are generated class versions, per-operation method versions, or fluent callback branches. Latest resolves to the highest available positive integer. Unknown operation names are rejected before execution; missing selected versions do not fall back.

SnapshotStore uses the standard options factory, including Configure/PostConfigure/validators, and subscribes directly to options change tokens. This permits rejected updates to be logged while retaining the last valid immutable dispatch snapshot; native IOptionsMonitor does not call subscribers when validation rejects a value. Candidates are validated under a lock and atomically published. Independent native options consumers keep their normal validation behavior.

Enabled host startup validation checks configuration, catalogs, and concrete registrations without constructing implementations. ValidateOnStart=false defers VersionFlow validation until runtime use; explicit options-builder ValidateOnStart remains authoritative. Configure delegates should be deterministic and side-effect free.

## Public API

Versioned(Type, int) declares a class version; Versioned(Type) declares an operations host; Versioned(string, int) declares an operation method version. The generator validates constructor/target combinations. Class-family and independent-method models cannot share a contract/key.

Versioned contracts must be non-generic. A non-generic CQRS wrapper inheriting ICqrsHandler<Request, Result> is supported, including inherited HandleAsync. Closed generic CQRS interfaces remain valid for ordinary DI and fluent callbacks but cannot be the Versioned contract directly.

Resolve always returns a facade. Operations hosts require a method for version introspection. Match/MatchAsync builders are immutable and resolve/execute only one selected branch. Separate introspection and invocation are not atomic across reloads. Dynamic callback branches declare availability at execution; AddVersionFlowOperation declares only names for fluent-only operations. Captured-selection replay and request-level pinning are not included.

## Diagnostics

| ID | Meaning |
| --- | --- |
| VF001 | Invalid contract or missing interface implementation |
| VF002 | Duplicate version |
| VF003 | Non-positive version |
| VF004 | Blank/conflicting/duplicate logical key |
| VF005 | Missing or unknown operation |
| VF006 | Incompatible signature or overloaded name |
| VF007 | Unsupported generic method/constraints |
| VF008 | Unsupported ref/out/in or ref return |
| VF009 | Unsupported interface member/pointer signature |
| VF010 | Conflicting models or misplaced method annotation |
| VF011 | Inaccessible or non-instantiable implementation |
| VF012 | Versioned overload invalid for target, or Key on method |

Generator output is deterministic, nullable-aware, and uses direct typed invocations. Generic contracts/implementation types, overloaded names, properties/events, and default/static interface members remain unsupported.

## Validation and release

The normal solution tests cover runtime, compiler diagnostics, automatic discovery, options/reload, DI ownership, and extension behavior. CI additionally packs and executes a Core-only package-consumer graph with local and referenced versioned services, using an isolated package cache and local source mapping. PackageSmoke projects stay outside the normal solution to avoid restoring an unpacked release.

Use dotnet build, dotnet test, and dotnet pack. Publish only Core, Extensions.DI, and Extensions.Cqrs. Each package embeds its README, MIT license, and icon. The generator is bundled into Core and is not a standalone package.
