# VersionFlow.Generator

Version **1.0.0** · Roslyn incremental source generator · MIT

Generates typed dispatchers and immutable catalog metadata for VersionFlow's service and method attributes. Dispatch uses direct typed calls and existing DI registrations, without reflection-based invocation.

## Install

In every project declaring versioned implementations:

```xml
<PackageReference Include="VersionFlow.Core" Version="1.0.0" />
```

When referencing the generator project from source, use `OutputItemType="Analyzer" ReferenceOutputAssembly="false"` on the `ProjectReference`.

The analyzer targets .NET Standard 2.0 for compiler compatibility and uses Roslyn 4.14 APIs. VersionFlow applications target .NET 10 or later. The Core package contains the generator DLL under `analyzers/dotnet/cs`, not as an application runtime dependency. This generator project is kept separate for compiler development and tests, but is not separately published.

## Usage

```csharp
using VersionFlow;

public interface IMessage { string Read(); }

[Versioned(typeof(IMessage), 1, Key = "Message")]
public sealed class MessageV1 : IMessage
{
    public string Read() => "Original";
}
```

For an assembly named `MyApp`, link its generated catalog during startup:

```csharp
services.AddScoped<MessageV1>();
services.AddVersionFlowCatalog<VersionFlow.Generated.Assembly_MyApp.GeneratedCatalog>();
services.AddVersionFlowOptions();
services.AddVersionFlowDispatchers();
```

Assembly-name characters other than letters and digits are replaced with underscores in the generated namespace. Link each referenced declaring assembly's catalog separately. There is no runtime assembly scanning or automatic concrete service registration. Use ordinary DI or the optional DI attribute extension for implementation registrations.

Independent method hosts use `[Versioned(typeof(IContract))]` on the class and `[Versioned("MethodName", version)]` on its methods. Generated calls support inherited ordinary interface methods, explicit interface implementations, nullable references, void, Task, and ValueTask return values.

## Diagnostics

| ID | Invalid declaration |
| --- | --- |
| VF001 | Invalid contract or missing interface implementation |
| VF002 | Duplicate version |
| VF003 | Non-positive version |
| VF004 | Blank, conflicting, or duplicate key |
| VF005 | Missing or unknown operation |
| VF006 | Incompatible signature or overloaded operation name |
| VF007 | Generic method or constraints |
| VF008 | ref/out/in parameter or ref return |
| VF009 | Unsupported interface member or pointer signature |
| VF010 | Conflicting versioning models or misplaced method annotation |
| VF011 | Inaccessible or non-instantiable implementation |
| VF012 | Wrong Versioned constructor for the target, or Key specified on a method |

Generic contracts/implementation types, properties/indexers/events, default/static interface members, and overload-based operation routing are unsupported. Attributes do not intercept direct calls or rewrite method bodies. Output is deterministic and nullable-aware.

## License

MIT. Copyright (c) 2026 Bradley Ables. The full license is included as `LICENSE`.



