# VersionFlow.PackageSmoke

A package-only executable that verifies the actual packed Core artifact, including its bundled generator. It is intentionally outside the solution: restore it only after packing.

CI runs this check automatically after packing, using ordinary dotnet commands, an isolated package cache, and a clean checkout. NuGet.Config restricts VersionFlow packages to the local artifact feed so a published package cannot mask a broken local artifact.

To run the same check manually from the repository root:

```sh
dotnet pack VersionFlow.slnx -c Release -o artifacts/packages
dotnet restore tests/VersionFlow.PackageSmoke/VersionFlow.PackageSmoke.csproj --configfile tests/VersionFlow.PackageSmoke/NuGet.Config
dotnet run --project tests/VersionFlow.PackageSmoke/VersionFlow.PackageSmoke.csproj -c Release --no-restore
```

Use an empty NUGET_PACKAGES directory when manually checking a rebuilt package with the same version. The root MIT license applies.
