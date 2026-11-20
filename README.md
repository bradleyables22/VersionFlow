# VersionFlow

Choose versions of DI-registered .NET services and their instance methods through configuration while calling ordinary typed interfaces. Version routing requires dependency injection; static methods and direct implementation calls are not routed.

Start with [VersionFlow](VersionFlow.Core/README.md) for installation and a working example.

## Packages

| Package | Documentation |
| --- | --- |
| [VersionFlow](VersionFlow.Core/README.md) | Typed version routing, configuration, feature flags, and getting started. |
| [VersionFlow.Extensions.DI](VersionFlow.Extensions.DI/README.md) | Attribute-based service registration. |
| [VersionFlow.Extensions.Cqrs](VersionFlow.Extensions.Cqrs/README.md) | Direct-call CQRS handler interfaces. |
| [VersionFlow.Extensions.Railway](VersionFLow.Extensions.Railway/README.md) | Typed results, validation, recovery, and operation chaining. |
| [VersionFlow.Extensions.Results](VersionFlow.Extensions.Results/README.md) | Operation outcomes and numbered, offset, or cursor pages. |
| [VersionFlow.Extensions.Results.AspNetCore](VersionFlow.Extensions.Results.AspNetCore/README.md) | HTTP response mapping for Results. |

## Development

- [Source generator (bundled with VersionFlow)](VersionFlow.Generator/README.md)
- [Tests](tests/VersionFlow.Tests/README.md)
- [Referenced-assembly test fixture](tests/VersionFlow.TestServices/README.md)
- [Package smoke application](tests/VersionFlow.PackageSmoke/README.md)
- [Package smoke library](tests/VersionFlow.PackageSmoke.Library/README.md)

Licensed under the [MIT license](LICENSE).
