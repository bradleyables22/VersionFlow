# VersionFlow.Tests

.NET 10 xUnit tests for VersionFlow 1.0.0. This project is not published as a NuGet package.

Run from the solution root:

```powershell
dotnet test VersionFlow.slnx -c Release
```

Coverage includes selection precedence, generated facades, DI lifetimes, once-only synchronous/asynchronous disposal across all lifetimes and interface bindings, options binding and reload, immutable fluent callbacks, compiler diagnostics, cross-assembly catalogs, independent CQRS contracts, and explicit/automatic interface bindings.

Generator tests compile in-memory C# with Roslyn. Integration tests link the separate `VersionFlow.TestServices` fixture assembly. The generator project is referenced both as an analyzer for test fixtures and as a normal reference for direct generator-driver tests.

The repository's root MIT license applies.

