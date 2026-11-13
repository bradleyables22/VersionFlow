using System.Collections.Immutable;
using Microsoft.Extensions.DependencyInjection;

namespace VersionFlow;

public interface IVersionFlowCatalog
{
    IEnumerable<VersionFlowContractDescriptor> Contracts { get; }
    IEnumerable<IVersionFlowCatalog> ReferencedCatalogs => [];
}
public sealed class VersionFlowContractDescriptor
{
    public Type ContractType { get; }
    public string Key { get; }
    public ImmutableDictionary<string, ImmutableArray<int>> Methods { get; }
    public ImmutableArray<int> Versions { get; }
    public ImmutableArray<Type> ImplementationTypes { get; }
    public Func<IServiceProvider, VersionChoice?, object> CreateFacade { get; }
    public bool IsOperationsHost { get; }
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
public sealed class VersionFlowCatalog
{
    public ImmutableDictionary<string, VersionFlowContractDescriptor> Contracts { get; }
    public ImmutableHashSet<string> FluentOperations { get; }
    public VersionFlowCatalog(IEnumerable<IVersionFlowCatalog> catalogs, IEnumerable<FluentOperationDeclaration> operations)
    {
        var all = catalogs.SelectMany(c => c.Contracts).ToArray();
        if (all.GroupBy(c => c.Key, StringComparer.Ordinal).Any(g => g.Count() > 1) || all.GroupBy(c => c.ContractType).Any(g => g.Count() > 1))
            throw new VersionFlowConfigurationException("Duplicate logical keys or contracts across generated catalogs.");
        Contracts = all.ToImmutableDictionary(c => c.Key, StringComparer.Ordinal);
        FluentOperations = operations.Select(o => o.Key).ToImmutableHashSet(StringComparer.Ordinal);
    }
    public VersionFlowContractDescriptor For(Type contract) => Contracts.Values.SingleOrDefault(c => c.ContractType == contract)
        ?? throw new VersionFlowRegistrationException($"No generated catalog linked for '{contract}'. Call AddVersionFlowCatalog with its generated catalog.");
}
public sealed record FluentOperationDeclaration(string Key);

/// <summary>Used by generated, strongly typed dispatchers.</summary>
public sealed class VersionFlowDispatcherContext(IServiceProvider provider, VersionFlowSelector selector)
{
    public VersionSelection Select(string key, string method, VersionChoice? choice) => selector.Select(key, method, choice);
    public T GetRequired<T>() where T : class
    {
        // Check registration separately so exceptions from user constructors propagate unchanged.
        if (provider.GetService<IServiceProviderIsService>() is { } registrations && !registrations.IsService(typeof(T)))
            throw new VersionFlowRegistrationException($"Selected implementation '{typeof(T)}' must be registered by its concrete type in DI. Interface-only registrations cannot select this version.");
        return provider.GetService<T>() ?? throw new VersionFlowRegistrationException($"Selected implementation '{typeof(T)}' is not registered in DI.");
    }
}

