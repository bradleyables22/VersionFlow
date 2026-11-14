# VersionFlow.PackageSmoke

Package-consumer executable for VersionFlow, intentionally outside the solution so ordinary restore does not require a previously packed release.

This project and its PackageSmoke.Library fixture reference only packed Core, never runtime/generator source projects. The consumer registers concrete implementations and calls AddVersionFlow without naming catalogs or registering dispatchers. It verifies automatic discovery in both projects, typed routing, method precedence, explicit overrides, fluent callbacks, and rejection of unknown names.

CI packs first, then restores/runs this project using a fresh cache and exclusive local VersionFlow source mapping. No PowerShell scripts are required.

From the repository root:

```sh
dotnet pack VersionFlow.slnx -c Release -o artifacts/packages
dotnet restore tests/VersionFlow.PackageSmoke/VersionFlow.PackageSmoke.csproj --configfile tests/VersionFlow.PackageSmoke/NuGet.Config
dotnet run --project tests/VersionFlow.PackageSmoke/VersionFlow.PackageSmoke.csproj -c Release --no-restore
```

When manually retesting a rebuilt package with the same version, use an empty NUGET_PACKAGES directory. The root MIT license applies.
