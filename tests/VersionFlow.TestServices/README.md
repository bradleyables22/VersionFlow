# VersionFlow.TestServices

.NET 10 integration-test fixture for VersionFlow **1.0.1**. Not published as a NuGet package.

The assembly declares IExternal versions and generates catalog metadata. Tests verify automatic discovery through referenced catalogs, explicitly selected discovery roots, the manual catalog API, and scope disposal.

```sh
dotnet test VersionFlow.slnx -c Release
```

The root MIT license applies.
