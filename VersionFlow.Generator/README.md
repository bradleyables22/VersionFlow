# VersionFlow.Generator

**compiler component · MIT**

This is a development project, not a separately published package. Its DLL is bundled in VersionFlow under analyzers/dotnet/cs. Applications install only:

```sh
dotnet add package VersionFlow
```

## What it generates

The incremental generator reads Versioned attributes and produces typed interface dispatchers, a catalog of implementations/method versions, assembly metadata identifying that catalog, and typed links to referenced generated catalogs.

With concrete implementations registered in DI, `services.AddVersionFlow(configurationSection)` discovers the catalog graph and registers scoped interface dispatchers automatically. Applications do not need to name generated classes or call AddVersionFlowDispatchers. No service implementations are constructed during catalog discovery.

```csharp
[Versioned(typeof(IMessage), 1, Key = "Message")]
public sealed class MessageV1 : IMessage
{
    public string Read() => "Original";
}
```

For independent methods, use Versioned(typeof(IContract)) on the host and Versioned(operationName, version) on its methods. Generated calls support inherited ordinary interface methods, explicit interface implementations, nullable references, void, Task, and ValueTask returns. Attributes do not rewrite method bodies or intercept concrete calls.

## Development and multiple assemblies

The generator targets .NET Standard for compiler hosting and uses Roslyn APIs. Runtime applications target .NET. Source-tree consumers reference this project as an analyzer with OutputItemType="Analyzer" and ReferenceOutputAssembly="false"; package consumers get it from VersionFlow automatically.

Each declaring assembly exposes a GeneratedCatalog under VersionFlow.Generated.Assembly_<sanitized assembly name>. Non-alphanumeric assembly-name characters become underscores. Avoid names that collide after sanitizing. Referenced generated catalogs are linked using compile-time symbols, and runtime discovery deduplicates their types. Rebuild startup and declaring projects to emit the catalog metadata. Dynamic plugins and arbitrary loaded assemblies are not scanned.

AddVersionFlow starts from the calling assembly; shared helpers can specify its assembly argument. Explicit catalog APIs remain for advanced/manual integrations with autoRegister:false. Native AOT is not certified.

## CQRS and generics

The Versioned contract must be non-generic, even if generic arguments are closed. Define `public interface IGetPriceHandler : ICqrsAsyncHandler<GetPrice, decimal> { }` and use Versioned(typeof(IGetPriceHandler), 1). The inherited HandleAsync method is supported. Passing typeof(ICqrsAsyncHandler<GetPrice, decimal>) directly is unsupported. Synchronous `ICqrsHandler` wrappers work the same way through their inherited `Handle` method. Both handler families support response-bearing and no-response contracts.

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
| VF012 | Wrong Versioned constructor for its target, or Key on a method |

MIT. Copyright (c) 2026 Bradley Ables. Full license included as LICENSE.
