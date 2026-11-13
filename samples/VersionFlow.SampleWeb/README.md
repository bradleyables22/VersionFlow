# VersionFlow.SampleWeb

.NET 10 sample application demonstrating VersionFlow 1.0.0, generated facades, attribute DI, independent method versions, and direct CQRS handlers. This project is not published as a NuGet package.

Run from the solution root:

```powershell
dotnet run --project samples/VersionFlow.SampleWeb --urls http://localhost:5129
```

`appsettings.json` selects the default and per-method versions. The application registers attributed concrete services, links its generated catalog, and installs scoped interface facades.

| Route | Expected result |
| --- | --- |
| `/` | Total, discount, operations total, and operations discount are all 95 |
| `/explicit/1` | 100 |
| `/orders/42` | `Order 42: v2, validate=True` |
| `/cqrs/10` | 95 |

The source demonstrates separate constructor dependencies, explicit version selection, callback branches with incompatible signatures, and a CQRS handler registered through the general DI extension.

The repository's root MIT license applies.
