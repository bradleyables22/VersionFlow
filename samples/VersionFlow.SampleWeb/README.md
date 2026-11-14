# VersionFlow.SampleWeb

.NET sample using VersionFlow. This application is not published as a NuGet package.

Run from the solution root:

```sh
dotnet run --project samples/VersionFlow.SampleWeb --urls http://localhost:5129
```

The standard startup is AddAttributedServices followed by AddVersionFlow(configurationSection). Core automatically discovers catalogs and installs interface dispatchers. The sample additionally declares Orders.Process because it demonstrates a fluent-only operation, and registers TimeProvider as a constructor dependency.

| Route | Expected result |
| --- | --- |
| `/` | Class total, discount, operations total, and operations discount are all 95 |
| `/explicit/1` | 100 |
| `/orders/42` | `Order 42: v2, validate=True` |
| `/cqrs/10` | 95 |

appsettings.json demonstrates class choices and method overrides. Pricing classes use concrete-only Service attributes; their interfaces are owned by generated dispatchers. The CQRS endpoint uses a non-versioned handler bound through ordinary attribute DI, which calls the versioned pricing facade. For versioning the CQRS handler itself, use the non-generic wrapper pattern in the root/Core/CQRS READMEs.

Source project references explicitly attach the generator for development. NuGet consumers receive it automatically from Core. The root MIT license applies.
