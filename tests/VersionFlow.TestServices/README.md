# VersionFlow.TestServices

.NET integration-test fixture for VersionFlow. Not published as a NuGet package.

The assembly declares IExternal versions and generates catalog metadata. Tests verify automatic discovery through referenced catalogs, explicitly selected discovery roots, the manual catalog API, and scope disposal.

```sh
dotnet test VersionFlow.slnx -c Release
```

The root MIT license applies.
