# VersionFlow.Tests

.NET xUnit tests for VersionFlow. This project is not published.

```sh
dotnet test VersionFlow.slnx -c Release
```

Tests cover automatic local/referenced catalog discovery, all setup styles, explicit manual opt-out, interface conflict detection, typed dispatch, selection precedence, options/reload validation, immutable fluent builders, and unknown-operation rejection. Other tests verify native DI lifetime/disposal behavior, CQRS contracts, interface binding, and Roslyn generator output/diagnostics.

VersionFlow.TestServices is the separate assembly fixture for catalog linking. Generator-driver tests reference the compiler project directly; application-style test fixtures consume it as an analyzer. The separate PackageSmoke projects are intentionally outside the solution and test actual packed artifacts in CI after packing.

The root MIT license applies.
RailwayTests covers result chaining, recovery and revalidation, typed error defaults, immutable error collections, exception handling, cancellation, and asynchronous operations.

ResultsTests covers immutable outcomes, collection snapshots, numbered/offset/cursor pagination boundaries, JSON diagnostic exclusion, strict timestamp cursor conversion, and executed HTTP responses including empty pages and Problem Details.

HostResultTests executes MVC results, checking typed controller compatibility, paging metadata, content types, and failure status/details.
