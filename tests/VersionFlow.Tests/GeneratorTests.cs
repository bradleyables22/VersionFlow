using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using VersionFlow.Generator;
using Xunit;
namespace VersionFlow.Tests;

public sealed class GeneratorTests
{
    private static readonly MetadataReference[] References = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
        .Append(typeof(VersionedAttribute).Assembly.Location)
        .Distinct(StringComparer.OrdinalIgnoreCase).Select(path => MetadataReference.CreateFromFile(path)).ToArray();
    private static (GeneratorDriverRunResult Run, Compilation Compilation) Generate(string code)
    {
        var parse = new CSharpParseOptions(LanguageVersion.Preview);
        var compilation = CSharpCompilation.Create("GeneratorFixture", [CSharpSyntaxTree.ParseText("#nullable enable\nusing VersionFlow;\n" + code, parse)], References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        GeneratorDriver driver = CSharpGeneratorDriver.Create([new VersionFlowGenerator().AsSourceGenerator()], parseOptions: parse);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var updated, out _);
        return (driver.GetRunResult(), updated);
    }
    [Fact] public void Output_is_deterministic_nullable_aware_and_compiles()
    {
        const string code = """
            public interface IBase { void Touch(); }
            public interface IThing : IBase { string? Echo(string? value); System.Threading.Tasks.Task<int> Work(System.Threading.CancellationToken ct); }
            [Versioned(typeof(IThing), 1)] public sealed class Thing : IThing
            {
                public void Touch() { }
                string? IThing.Echo(string? value) => value;
                public System.Threading.Tasks.Task<int> Work(System.Threading.CancellationToken ct) => System.Threading.Tasks.Task.FromResult(1);
            }
            """;
        var first = Generate(code); var second = Generate(code);
        Assert.Empty(first.Run.Diagnostics);
        Assert.Empty(first.Compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error || d.Severity == DiagnosticSeverity.Warning));
        Assert.Equal(first.Run.GeneratedTrees.Single().ToString(), second.Run.GeneratedTrees.Single().ToString());
        Assert.Contains("GetRequired<", first.Run.GeneratedTrees.Single().ToString());
        Assert.DoesNotContain("Reflection", first.Run.GeneratedTrees.Single().ToString());
    }
    public static TheoryData<string, string> InvalidDeclarations => new()
    {
        { "VF001", "public interface I { int M(); } [Versioned(typeof(I),1)] public class C { }" },
        { "VF002", "public interface I { int M(); } [Versioned(typeof(I),1)] public class C : I { public int M()=>1; } [Versioned(typeof(I),1)] public class D : I { public int M()=>2; }" },
        { "VF003", "public interface I { int M(); } [Versioned(typeof(I),0)] public class C : I { public int M()=>1; }" },
        { "VF004", "public interface I { int M(); } [Versioned(typeof(I),1,Key=\" \" )] public class C : I { public int M()=>1; }" },
        { "VF004", "public interface I { int M(); } [Versioned(typeof(I),1,Key=\"A\")] public class C : I { public int M()=>1; } [Versioned(typeof(I),2,Key=\"B\")] public class D : I { public int M()=>2; }" },
        { "VF004", "public interface I { int M(); } public interface J { int M(); } [Versioned(typeof(I),1,Key=\"A\")] public class C : I { public int M()=>1; } [Versioned(typeof(J),1,Key=\"A\")] public class D : J { public int M()=>2; }" },
        { "VF005", "public interface I { int M(); } [Versioned(typeof(I))] public class C { }" },
        { "VF006", "public interface I { int M(int x); } [Versioned(typeof(I))] public class C { [Versioned(\"M\",1)] public string V(int x)=>\"x\"; }" },
        { "VF006", "public interface I { int M(int x); int M(string x); } [Versioned(typeof(I),1)] public class C : I { public int M(int x)=>1; public int M(string x)=>1; }" },
        { "VF007", "public interface I { T M<T>() where T : class; } [Versioned(typeof(I),1)] public class C : I { public T M<T>() where T : class => default!; }" },
        { "VF008", "public interface I { int M(ref int x); } [Versioned(typeof(I),1)] public class C : I { public int M(ref int x)=>1; }" },
        { "VF008", "public interface I { ref int M(); } [Versioned(typeof(I),1)] public class C : I { int x; public ref int M()=>ref x; }" },
        { "VF009", "public interface I { int Value { get; } } [Versioned(typeof(I),1)] public class C : I { public int Value=>1; }" },
        { "VF009", "public interface I { int this[int i] { get; } } [Versioned(typeof(I),1)] public class C : I { public int this[int i]=>1; }" },
        { "VF009", "public interface I { event System.Action Changed; } [Versioned(typeof(I),1)] public class C : I { public event System.Action Changed = delegate {}; }" },
        { "VF009", "public interface I { int M()=>1; } [Versioned(typeof(I),1)] public class C : I { }" },
        { "VF010", "public interface I { int M(); } [Versioned(typeof(I),1)] [Versioned(typeof(I))] public class C : I { public int M()=>1; }" },
        { "VF010", "public interface I { int M(); } [Versioned(typeof(I),1)] public class C : I { public int M()=>1; } [Versioned(typeof(I))] public class D { [Versioned(\"M\",1)] public int V()=>1; }" },
        { "VF010", "public class C { [Versioned(\"M\",1)] public int V()=>1; }" },
        { "VF011", "public interface I { int M(); } [Versioned(typeof(I),1)] public abstract class C : I { public int M()=>1; }" },
        { "VF011", "public interface I { int M(); } [Versioned(typeof(I),1)] public class C : I { private C() {} public int M()=>1; }" },
        { "VF002", "public interface I { int M(); } [Versioned(typeof(I))] public class C { [Versioned(\"M\",1)] public int A()=>1; [Versioned(\"M\",1)] public int B()=>2; }" },
        { "VF003", "public interface I { int M(); } [Versioned(typeof(I))] public class C { [Versioned(\"M\",0)] public int A()=>1; }" },
        { "VF005", "public interface I { int M(); } [Versioned(typeof(I))] public class C { [Versioned(\"Unknown\",1)] public int A()=>1; }" },
    };
    [Theory] [MemberData(nameof(InvalidDeclarations))]
    public void Unsupported_declarations_produce_actionable_diagnostics(string expected, string code)
    {
        var result = Generate(code);
        Assert.Contains(result.Run.Diagnostics, d => d.Id == expected && d.Severity == DiagnosticSeverity.Error && d.Location.IsInSource);
        Assert.DoesNotContain(result.Run.Diagnostics, d => d.Id == "CS8785");
    }
    [Theory]
    [InlineData("[Versioned(\"M\", 1)] public class C { }")]
    [InlineData("public interface I { int M(); } [Versioned(typeof(I))] public class C { [Versioned(typeof(I), 1)] public int M() => 1; }")]
    [InlineData("public interface I { int M(); } [Versioned(typeof(I))] public class C { [Versioned(typeof(I))] public int M() => 1; }")]
    [InlineData("public interface I { int M(); } [Versioned(typeof(I))] public class C { [Versioned(\"M\", 1, Key = \"bad\")] public int M() => 1; }")]
    public void Unified_attribute_reports_wrong_target_overloads(string code)
    {
        var result = Generate(code);
        Assert.Contains(result.Run.Diagnostics, d => d.Id == "VF012");
        Assert.DoesNotContain(result.Run.Diagnostics, d => d.Id == "CS8785");
    }

    [Fact]
    public void Unified_attribute_supports_aliases_and_named_constructor_arguments()
    {
        var result = Generate("""
            using V = VersionFlow.VersionedAttribute;
            public interface I { int M(); }
            [V(version: 2, contractType: typeof(I))] public class C : I { public int M() => 2; }
            public interface IOps { int M(); }
            [V(contractType: typeof(IOps))] public class Host
            {
                [V(version: 3, operation: nameof(IOps.M))] public int Third() => 3;
            }
            """);
        Assert.Empty(result.Run.Diagnostics);
        Assert.Empty(result.Compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error));
    }

    [Fact]
    public void Unified_attribute_exposes_the_declared_mode_metadata()
    {
        var service = new VersionedAttribute(typeof(IDisposable), 2) { Key = "Service" };
        Assert.Equal(typeof(IDisposable), service.ContractType);
        Assert.Equal(2, service.Version);
        Assert.Null(service.Operation);
        Assert.Equal("Service", service.Key);
        var host = new VersionedAttribute(typeof(IDisposable));
        Assert.Equal(typeof(IDisposable), host.ContractType);
        Assert.Null(host.Version);
        Assert.Null(host.Operation);
        var method = new VersionedAttribute("Dispose", 3);
        Assert.Null(method.ContractType);
        Assert.Equal("Dispose", method.Operation);
        Assert.Equal(3, method.Version);
    }
    [Theory]
    [InlineData("ICqrsHandler<GetPrice, decimal>", "decimal", "Handle", "request.Quantity * 10m")]
    [InlineData("ICqrsHandler<GetPrice>", "void", "Handle", "System.GC.KeepAlive(request)")]
    [InlineData("ICqrsAsyncHandler<GetPrice, decimal>", "System.Threading.Tasks.Task<decimal>", "HandleAsync", "System.Threading.Tasks.Task.FromResult(request.Quantity * 10m)")]
    [InlineData("ICqrsAsyncHandler<GetPrice>", "System.Threading.Tasks.Task", "HandleAsync", "System.Threading.Tasks.Task.CompletedTask")]
    public void Non_generic_cqrs_wrapper_generates_inherited_handler_method(
        string contract, string returnType, string method, string expression)
    {
        var result = Generate($$"""
            public sealed record GetPrice(int Quantity);
            public interface IGetPriceHandler : global::VersionFlow.Extensions.Cqrs.{{contract}} { }
            [Versioned(typeof(IGetPriceHandler), 1, Key = "Pricing")]
            public sealed class GetPriceV1 : IGetPriceHandler
            {
                public {{returnType}} {{method}}(GetPrice request, System.Threading.CancellationToken ct = default)
                    => {{expression}};
            }
            """);
        Assert.Empty(result.Run.Diagnostics);
        Assert.Empty(result.Compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error));
        Assert.Contains(method, result.Run.GeneratedTrees.Single().ToString());
    }
    [Fact] public void Independent_methods_generate_compilable_typed_calls()
    {
        var result = Generate("""
            public interface I { int M(int x); void Touch(); }
            [Versioned(typeof(I), Key="Operations")] public class C
            {
                [Versioned("M", 1)] public int First(int x) => x;
                [Versioned("M", 4)] public int Latest(int x) => x * 4;
                [Versioned("Touch", 2)] public void Touch2() { }
            }
            """);
        Assert.Empty(result.Run.Diagnostics);
        Assert.Empty(result.Compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error));
    }
}



