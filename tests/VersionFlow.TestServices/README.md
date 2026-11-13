# VersionFlow.TestServices

.NET 10 integration-test fixture for VersionFlow 1.0.0. This project is not published as a NuGet package.

The assembly declares versioned `IExternal` implementations and generates its own catalog. `VersionFlow.Tests` links that catalog to verify cross-assembly routing, concrete DI resolution, and scoped disposal.

Build and exercise it from the solution root:

```powershell
dotnet test VersionFlow.slnx -c Release
```

The repository's root MIT license applies.
