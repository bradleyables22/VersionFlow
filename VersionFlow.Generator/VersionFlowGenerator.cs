using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace VersionFlow.Generator;

[Generator(LanguageNames.CSharp)]
public sealed class VersionFlowGenerator : IIncrementalGenerator
{
    private const string AttributeName = "VersionFlow.VersionedAttribute";
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var classes = context.SyntaxProvider.CreateSyntaxProvider(
            static (node, _) => node is TypeDeclarationSyntax t && t.AttributeLists.Count > 0,
            static (ctx, ct) => ctx.SemanticModel.GetDeclaredSymbol((TypeDeclarationSyntax)ctx.Node, ct) as INamedTypeSymbol)
            .Where(static t => t != null && t.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == AttributeName));
        context.RegisterSourceOutput(classes.Collect().Combine(context.CompilationProvider.Select(static (c, _) => c.AssemblyName ?? "Assembly")),
            static (ctx, input) => Generate(ctx, input.Left, input.Right));
        var orphanMethods = context.SyntaxProvider.ForAttributeWithMetadataName(AttributeName,
            static (node, _) => node is MethodDeclarationSyntax,
            static (ctx, _) => (IMethodSymbol)ctx.TargetSymbol);
        context.RegisterSourceOutput(orphanMethods, static (ctx, method) =>
        {
            foreach (var attribute in method.GetAttributes().Where(a => a.AttributeClass?.ToDisplayString() == AttributeName))
                if (!IsMethodVersion(attribute) || attribute.NamedArguments.Any(p => p.Key == "Key"))
                    Error(ctx, "VF012", method, "On methods use [Versioned(operationName, version)] without Key.");
            if (!method.ContainingType.GetAttributes().Any(IsOperationsHost))
                Error(ctx, "VF010", method, "A versioned method requires a [Versioned(typeof(IContract))] host; class-family/method matrices are unsupported.");
        });
    }
    private static bool IsOperationsHost(AttributeData attribute) => HasSignature(attribute, 1, "System.Type");
    private static bool IsClassVersion(AttributeData attribute) => HasSignature(attribute, 2, "System.Type");
    private static bool IsMethodVersion(AttributeData attribute) => HasSignature(attribute, 2, "string");
    private static bool HasSignature(AttributeData attribute, int count, string firstType) =>
        attribute.AttributeClass?.ToDisplayString() == AttributeName &&
        attribute.AttributeConstructor is IMethodSymbol constructor && constructor.Parameters.Length == count &&
        constructor.Parameters[0].Type.ToDisplayString() == firstType && attribute.ConstructorArguments.Length == count;
    private static void Error(SourceProductionContext ctx, string id, ISymbol symbol, string message) =>
        ctx.ReportDiagnostic(Diagnostic.Create(new DiagnosticDescriptor(id, "Invalid VersionFlow declaration", "{0}", "VersionFlow", DiagnosticSeverity.Error, true), symbol.Locations.FirstOrDefault(), message));
    private static string Type(ITypeSymbol symbol) => symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat.WithMiscellaneousOptions(SymbolDisplayMiscellaneousOptions.UseSpecialTypes | SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers | SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier));
    private static string Literal(string value) => SymbolDisplay.FormatLiteral(value, true);
    private static bool Accessible(INamedTypeSymbol type)
    {
        for (var t = type; t != null; t = t.ContainingType)
            if (t.IsGenericType || t.IsFileLocal || (t.DeclaredAccessibility != Accessibility.Public && t.DeclaredAccessibility != Accessibility.Internal)) return false;
        return true;
    }
    private sealed class Entry
    {
        public INamedTypeSymbol Implementation = null!;
        public INamedTypeSymbol Contract = null!;
        public string Key = "";
        public int Version;
        public bool Operations;
    }
    private static void Generate(SourceProductionContext ctx, ImmutableArray<INamedTypeSymbol?> symbols, string assemblyName)
    {
        var entries = new List<Entry>();
        foreach (var symbol in symbols.Distinct(SymbolEqualityComparer.Default).OfType<INamedTypeSymbol>().OrderBy(t => t.ToDisplayString(), StringComparer.Ordinal))
        {
            var attributes = symbol.GetAttributes().Where(a => a.AttributeClass?.ToDisplayString() == AttributeName).ToArray();
            if (attributes.Length != 1) { Error(ctx, "VF010", symbol, "Class-family and operations-host models cannot be combined."); continue; }
            var attribute = attributes[0];
            if (!IsOperationsHost(attribute) && !IsClassVersion(attribute))
            { Error(ctx, "VF012", symbol, "On classes use [Versioned(typeof(IContract), version)] or [Versioned(typeof(IContract))]."); continue; }
            var contract = attribute.ConstructorArguments.FirstOrDefault().Value as INamedTypeSymbol;
            var operations = IsOperationsHost(attribute);
            if (contract == null || contract.TypeKind != TypeKind.Interface || !Accessible(contract))
            { Error(ctx, "VF001", symbol, "Contract must be an accessible, non-generic interface."); continue; }
            if (symbol.TypeKind != TypeKind.Class || symbol.IsAbstract || symbol.IsStatic || !Accessible(symbol) || !symbol.InstanceConstructors.Any(c => c.DeclaredAccessibility == Accessibility.Public))
            { Error(ctx, "VF011", symbol, "Implementation must be an accessible, non-generic concrete class with a public constructor."); continue; }
            if (!operations && !symbol.AllInterfaces.Contains(contract, SymbolEqualityComparer.Default))
            { Error(ctx, "VF001", symbol, "Class version must implement its declared contract."); continue; }
            var key = attribute.NamedArguments.Any(p => p.Key == "Key") ? attribute.NamedArguments.First(p => p.Key == "Key").Value.Value as string : contract.ToDisplayString();
            if (string.IsNullOrWhiteSpace(key)) { Error(ctx, "VF004", symbol, "Logical key cannot be blank."); continue; }
            var version = operations ? 0 : (int)(attribute.ConstructorArguments[1].Value ?? 0);
            if (!operations && version <= 0) { Error(ctx, "VF003", symbol, "Version numbers must be positive integers."); continue; }
            entries.Add(new Entry { Implementation = symbol, Contract = contract, Key = key!, Version = version, Operations = operations });
        }
        var badKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var group in entries.GroupBy(e => e.Key, StringComparer.Ordinal))
            if (group.Select(e => e.Contract).Distinct(SymbolEqualityComparer.Default).Count() > 1)
            { Error(ctx, "VF004", group.First().Implementation, "Logical key is shared by different contracts: " + group.Key); badKeys.Add(group.Key); }
        foreach (var group in entries.GroupBy(e => e.Contract, SymbolEqualityComparer.Default))
            if (group.Select(e => e.Key).Distinct(StringComparer.Ordinal).Count() > 1)
            { Error(ctx, "VF004", group.First().Implementation, "One contract cannot have conflicting logical keys."); foreach (var e in group) badKeys.Add(e.Key); }
        var namespaceName = "VersionFlow.Generated." + Sanitize(assemblyName);
        var source = new StringBuilder("// <auto-generated/>\n#nullable enable\nnamespace " + namespaceName + " {\n");
        var descriptors = new List<string>();
        var index = 0;
        foreach (var family in entries.GroupBy(e => e.Key, StringComparer.Ordinal).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            if (badKeys.Contains(family.Key)) continue;
            var versions = family.OrderBy(e => e.Version).ToArray();
            var first = versions[0];
            if (versions.Any(e => e.Operations) && (versions.Length != 1 || versions.Any(e => !e.Operations)))
            { Error(ctx, "VF010", first.Implementation, "A contract/key must have either class versions or exactly one operations host."); continue; }
            if (!first.Operations && versions.GroupBy(e => e.Version).Any(g => g.Count() > 1))
            { Error(ctx, "VF002", first.Implementation, "Duplicate class version in '" + family.Key + "'."); continue; }
            var members = first.Contract.AllInterfaces.Concat(new[] { first.Contract }).SelectMany(t => t.GetMembers()).Distinct(SymbolEqualityComparer.Default).ToArray();
            var methods = members.OfType<IMethodSymbol>().Where(m => m.MethodKind == MethodKind.Ordinary).OrderBy(m => m.Name, StringComparer.Ordinal).ToArray();
            var invalid = false;
            foreach (var member in members)
            {
                if (member is IMethodSymbol method && method.MethodKind == MethodKind.Ordinary)
                {
                    if (method.IsGenericMethod) { Error(ctx, "VF007", method, "Generic interface methods and constraints are unsupported in v1."); invalid = true; }
                    else if (method.ReturnsByRef || method.ReturnsByRefReadonly || method.Parameters.Any(p => p.RefKind != RefKind.None))
                    { Error(ctx, "VF008", method, "ref/out/in parameters and ref returns are unsupported in v1."); invalid = true; }
                    else if (method.IsStatic || !method.IsAbstract || method.DeclaredAccessibility != Accessibility.Public || Unsafe(method.ReturnType) || method.Parameters.Any(p => Unsafe(p.Type)))
                    { Error(ctx, "VF009", method, "Static, non-public, default interface methods and pointer signatures are unsupported in v1."); invalid = true; }
                }
                else if (member is IMethodSymbol unsupported && unsupported.MethodKind != MethodKind.PropertyGet && unsupported.MethodKind != MethodKind.PropertySet && unsupported.MethodKind != MethodKind.EventAdd && unsupported.MethodKind != MethodKind.EventRemove)
                { Error(ctx, "VF009", member, "Operators and non-ordinary interface methods are unsupported in v1."); invalid = true; }
                else if (member is IPropertySymbol || member is IEventSymbol)
                { Error(ctx, "VF009", member, "Properties, indexers and events are unsupported in v1 contracts; use ordinary methods."); invalid = true; }
            }
            if (methods.Length == 0) { Error(ctx, "VF005", first.Contract, "Contract must declare at least one operation."); invalid = true; }
            if (methods.GroupBy(m => m.Name, StringComparer.Ordinal).Any(g => g.Count() > 1))
            { Error(ctx, "VF006", first.Contract, "Overloaded or hidden operation names are unsupported; use unique method names for stable configuration keys."); invalid = true; }
            var routes = new Dictionary<string, List<Tuple<int, INamedTypeSymbol, IMethodSymbol>>>(StringComparer.Ordinal);
            foreach (var method in methods) if (!routes.ContainsKey(method.Name)) routes.Add(method.Name, new List<Tuple<int, INamedTypeSymbol, IMethodSymbol>>());
            if (first.Operations)
            {
                foreach (var implementation in first.Implementation.GetMembers().OfType<IMethodSymbol>())
                {
                    var attr = implementation.GetAttributes().FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == AttributeName);
                    if (attr == null) continue;
                    if (!IsMethodVersion(attr) || attr.NamedArguments.Any(p => p.Key == "Key")) { invalid = true; continue; }
                    var operation = attr.ConstructorArguments[0].Value as string ?? "";
                    var version = (int)(attr.ConstructorArguments[1].Value ?? 0);
                    if (version <= 0) { Error(ctx, "VF003", implementation, "Method version must be positive."); invalid = true; continue; }
                    var contractMethod = methods.FirstOrDefault(m => m.Name == operation);
                    if (contractMethod == null) { Error(ctx, "VF005", implementation, "Unknown contract operation '" + operation + "'."); invalid = true; continue; }
                    if (implementation.IsStatic || implementation.DeclaredAccessibility != Accessibility.Public || implementation.IsGenericMethod || implementation.ReturnsByRef || implementation.ReturnsByRefReadonly || !SymbolEqualityComparer.IncludeNullability.Equals(implementation.ReturnType, contractMethod.ReturnType) || implementation.Parameters.Length != contractMethod.Parameters.Length || implementation.Parameters.Where((p, i) => p.RefKind != RefKind.None || !SymbolEqualityComparer.IncludeNullability.Equals(p.Type, contractMethod.Parameters[i].Type)).Any())
                    { Error(ctx, "VF006", implementation, "Annotated method must be public, non-static and match the contract parameter and return types exactly."); invalid = true; continue; }
                    if (routes[operation].Any(r => r.Item1 == version)) { Error(ctx, "VF002", implementation, "Duplicate version for operation '" + operation + "'."); invalid = true; continue; }
                    routes[operation].Add(Tuple.Create(version, first.Implementation, implementation));
                }
                foreach (var method in methods)
                    if (routes[method.Name].Count == 0) { Error(ctx, "VF005", first.Implementation, "Missing implementations for '" + method.Name + "'."); invalid = true; }
            }
            else
                foreach (var method in methods)
                    foreach (var version in versions)
                    {
                        if (version.Implementation.FindImplementationForInterfaceMember(method) == null)
                        { Error(ctx, "VF001", version.Implementation, "Missing interface implementation for '" + method.Name + "'."); invalid = true; }
                        routes[method.Name].Add(Tuple.Create(version.Version, version.Implementation, method));
                    }
            if (invalid) continue;
            var facade = "Dispatcher" + index++;
            source.Append("internal sealed class ").Append(facade).Append(" : ").Append(Type(first.Contract)).Append(" {\nprivate readonly global::VersionFlow.VersionFlowDispatcherContext context;\nprivate readonly global::VersionFlow.VersionChoice? choice;\npublic ").Append(facade).Append("(global::System.IServiceProvider provider, global::VersionFlow.VersionChoice? choice) { this.context = global::Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<global::VersionFlow.VersionFlowDispatcherContext>(provider); this.choice = choice; }\n");
            foreach (var method in methods)
            {
                source.Append("public ").Append(Type(method.ReturnType)).Append(" @").Append(method.Name).Append('(')
                    .Append(string.Join(", ", method.Parameters.Select((p, i) => Type(p.Type) + " p" + i))).Append(") {\nswitch(context.Select(").Append(Literal(first.Key)).Append(", ").Append(Literal(method.Name)).Append(", choice).Number) {\n");
                foreach (var route in routes[method.Name].OrderBy(r => r.Item1))
                {
                    source.Append("case ").Append(route.Item1).Append(": ").Append(method.ReturnsVoid ? "" : "return ");
                    if (!first.Operations) source.Append("((").Append(Type(method.ContainingType)).Append(")");
                    source.Append("context.GetRequired<").Append(Type(route.Item2)).Append(">()");
                    if (!first.Operations) source.Append(')');
                    source.Append(".@").Append(route.Item3.Name).Append('(').Append(string.Join(", ", method.Parameters.Select((_, i) => "p" + i))).Append(");").Append(method.ReturnsVoid ? " return;" : "").Append('\n');
                }
                source.Append("default: throw new global::VersionFlow.VersionFlowSelectionException(\"Generated dispatch selection was unavailable.\"); } }\n");
            }
            source.Append("}\n");
            descriptors.Add("new global::VersionFlow.VersionFlowContractDescriptor(typeof(" + Type(first.Contract) + "), " + Literal(first.Key) + ", new global::System.Collections.Generic.Dictionary<string, int[]> { " + string.Join(", ", routes.OrderBy(r => r.Key, StringComparer.Ordinal).Select(r => "[" + Literal(r.Key) + "] = new int[] { " + string.Join(", ", r.Value.Select(v => v.Item1).OrderBy(v => v)) + " }")) + " }, new global::System.Type[] { " + string.Join(", ", versions.Select(v => "typeof(" + Type(v.Implementation) + ")")) + " }, static (provider, choice) => new " + facade + "(provider, choice), " + (first.Operations ? "true" : "false") + ")");
        }
        source.Append("public sealed class GeneratedCatalog : global::VersionFlow.IVersionFlowCatalog { public global::System.Collections.Generic.IEnumerable<global::VersionFlow.VersionFlowContractDescriptor> Contracts { get; } = new global::VersionFlow.VersionFlowContractDescriptor[] { ")
            .Append(string.Join(",\n", descriptors)).Append(" }; }\n}\n");
        ctx.AddSource("VersionFlow.Catalog.g.cs", source.ToString());
    }
    private static bool Unsafe(ITypeSymbol type) => type.TypeKind == TypeKind.Pointer || type.TypeKind == TypeKind.FunctionPointer || type is IArrayTypeSymbol a && Unsafe(a.ElementType);
    private static string Sanitize(string value) => "Assembly_" + string.Concat(value.Select(c => char.IsLetterOrDigit(c) ? c : '_'));
}



