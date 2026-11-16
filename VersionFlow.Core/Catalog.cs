using System.Collections.Immutable;
using Microsoft.Extensions.DependencyInjection;

namespace VersionFlow;

/// <summary>Generated metadata for one assembly. Prefer AddVersionFlow automatic discovery over implementing this yourself.</summary>
public interface IVersionFlowCatalog
{
    /// <summary>The contracts and available versions declared in this assembly.</summary>
    IEnumerable<VersionFlowContractDescriptor> Contracts { get; }
    /// <summary>Generated catalogs from referenced assemblies, followed during automatic discovery.</summary>
    IEnumerable<IVersionFlowCatalog> ReferencedCatalogs => [];
}
/// <summary>Immutable generated metadata and a facade factory for a single versioned contract.</summary>
public sealed class VersionFlowContractDescriptor
{
    /// <summary>The non-generic interface exposed to callers.</summary>
    public Type ContractType { get; }
    /// <summary>The case-sensitive logical key used by configuration and selection.</summary>
    public string Key { get; }
    /// <summary>Short method names mapped to their sorted, distinct available versions.</summary>
    public ImmutableDictionary<string, ImmutableArray<int>> Methods { get; }
    /// <summary>The sorted union of all method version numbers.</summary>
    public ImmutableArray<int> Versions { get; }
    /// <summary>All concrete implementation types that must be registered in DI.</summary>
    public ImmutableArray<Type> ImplementationTypes { get; }
    /// <summary>Creates a typed facade using the supplied scope and optional explicit choice.</summary>
    public Func<IServiceProvider, VersionChoice?, object> CreateFacade { get; }
    /// <summary>Whether methods have independent version sets, requiring a method name for inspection.</summary>
    public bool IsOperationsHost { get; }
    /// <summary>Builds metadata snapshots for generated dispatch. Application code normally uses generated catalogs.</summary>
    /// <param name="contractType">The routed interface.</param>
    /// <param name="key">A nonblank, case-sensitive logical key.</param>
    /// <param name="methods">Short method names with nonempty arrays of positive version numbers.</param>
    /// <param name="implementationTypes">Concrete types required in DI.</param>
    /// <param name="createFacade">Factory receiving the current scope and optional explicit version choice.</param>
    /// <param name="isOperationsHost">True when methods are independently versioned.</param>
    public VersionFlowContractDescriptor(Type contractType, string key, IDictionary<string, int[]> methods,
        Type[] implementationTypes, Func<IServiceProvider, VersionChoice?, object> createFacade, bool isOperationsHost = false)
    {
        ContractType = contractType; Key = key; CreateFacade = createFacade; IsOperationsHost = isOperationsHost;
        Methods = methods.ToImmutableDictionary(x => x.Key, x => x.Value.Distinct().Order().ToImmutableArray(), StringComparer.Ordinal);
        Versions = Methods.Values.SelectMany(x => x).Distinct().Order().ToImmutableArray();
        ImplementationTypes = implementationTypes.ToImmutableArray();
        if (string.IsNullOrWhiteSpace(key) || Methods.Count == 0 || Methods.Any(x => x.Value.IsEmpty || x.Value.Any(v => v <= 0)))
            throw new VersionFlowConfigurationException($"Invalid catalog entry '{key}'.");
    }
}
/// <summary>Aggregates linked generated contracts and declared fluent operations. Registered by AddVersionFlow.</summary>
public sealed class VersionFlowCatalog
{
    /// <summary>Linked contract descriptors indexed by case-sensitive logical key.</summary>
    public ImmutableDictionary<string, VersionFlowContractDescriptor> Contracts { get; }
    /// <summary>Declared, case-sensitive fluent-only operation names.</summary>
    public ImmutableHashSet<string> FluentOperations { get; }
    /// <summary>Combines explicitly supplied catalogs and operation declarations, rejecting duplicate keys or contracts.</summary>
    /// <param name="catalogs">Already-linked catalogs; this constructor does not traverse references.</param>
    /// <param name="operations">Names registered with AddVersionFlowOperation.</param>
    public VersionFlowCatalog(IEnumerable<IVersionFlowCatalog> catalogs, IEnumerable<FluentOperationDeclaration> operations)
    {
        var all = catalogs.SelectMany(c => c.Contracts).ToArray();
        if (all.GroupBy(c => c.Key, StringComparer.Ordinal).Any(g => g.Count() > 1) || all.GroupBy(c => c.ContractType).Any(g => g.Count() > 1))
            throw new VersionFlowConfigurationException("Duplicate logical keys or contracts across generated catalogs.");
        Contracts = all.ToImmutableDictionary(c => c.Key, StringComparer.Ordinal);
        FluentOperations = operations.Select(o => o.Key).ToImmutableHashSet(StringComparer.Ordinal);
    }
    /// <summary>Looks up generated metadata for a contract interface.</summary>
    /// <param name="contract">The exact contract type.</param>
    /// <returns>The linked descriptor.</returns>
    /// <exception cref="VersionFlowRegistrationException">No catalog contains the contract.</exception>
    public VersionFlowContractDescriptor For(Type contract) => Contracts.Values.SingleOrDefault(c => c.ContractType == contract)
        ?? throw new VersionFlowRegistrationException($"No generated catalog linked for '{contract}'. Call AddVersionFlowCatalog with its generated catalog.");
}
/// <summary>DI metadata declaring a fluent-only operation. Use AddVersionFlowOperation to register it.</summary>
/// <param name="Key">The qualified, case-sensitive operation name.</param>
public sealed record FluentOperationDeclaration(string Key);

/// <summary>Used by generated, strongly typed dispatchers.</summary>
/// <param name="provider">The DI scope from which selected concrete implementations are resolved.</param>
/// <param name="selector">The registered selector that applies current configuration and explicit choices.</param>
public sealed class VersionFlowDispatcherContext(IServiceProvider provider, VersionFlowSelector selector)
{
    /// <summary>Resolves a generated method's version without activating its implementation.</summary>
    /// <param name="key">The logical contract key.</param>
    /// <param name="method">The short method name.</param>
    /// <param name="choice">An explicit choice or null to follow configuration.</param>
    /// <returns>The selection from one current configuration snapshot.</returns>
    public VersionSelection Select(string key, string method, VersionChoice? choice) => selector.Select(key, method, choice);
    /// <summary>Resolves the selected concrete service from the current DI scope.</summary>
    /// <typeparam name="T">The concrete implementation type registered in DI.</typeparam>
    /// <returns>The DI-managed instance.</returns>
    /// <exception cref="VersionFlowRegistrationException">The concrete type is not registered.</exception>
    /// <remarks>Constructor exceptions propagate unchanged. No implicit activation is attempted.</remarks>
    public T GetRequired<T>() where T : class
    {
        // Check registration separately so exceptions from user constructors propagate unchanged.
        if (provider.GetService<IServiceProviderIsService>() is { } registrations && !registrations.IsService(typeof(T)))
            throw new VersionFlowRegistrationException($"Selected implementation '{typeof(T)}' must be registered by its concrete type in DI. Interface-only registrations cannot select this version.");
        return provider.GetService<T>() ?? throw new VersionFlowRegistrationException($"Selected implementation '{typeof(T)}' is not registered in DI.");
    }
}

