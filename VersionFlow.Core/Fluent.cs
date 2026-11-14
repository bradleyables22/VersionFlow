using System.Collections.Immutable;
using Microsoft.Extensions.DependencyInjection;
namespace VersionFlow;

internal sealed class VersionFlowRuntime(IServiceProvider provider, VersionFlowCatalog catalog, VersionFlowSelector selector, SnapshotStore store) : IVersionFlow
{
    public bool IsEnabled(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return store.Current.Flags.TryGetValue(name, out var enabled) && enabled;
    }
    public IVersionFlowContract<T> For<T>() where T : class => new Contract<T>(provider, catalog.For(typeof(T)), selector, null);
    public VersionFlowOperation For(string operationKey) => new(provider, selector, operationKey, null);
    public VersionSelection Select(string key, string? method = null) => selector.Select(key, method);
    private sealed record Contract<T>(IServiceProvider Provider, VersionFlowContractDescriptor Descriptor, VersionFlowSelector Selector, VersionChoice? Choice) : IVersionFlowContract<T> where T : class
    {
        public T Resolve() => (T)Descriptor.CreateFacade(Provider, Choice);
        public IVersionFlowContract<T> UseVersion(int version) => this with { Choice = VersionChoice.FromNumber(version) };
        public IVersionFlowContract<T> UseLatest() => this with { Choice = VersionChoice.Latest };
        public VersionSelection Selection => Selector.Select(Descriptor.Key, choice: Choice);
    }
}
public sealed class VersionFlowOperation : IVersionFlowOperation
{
    private readonly IServiceProvider provider;
    private readonly VersionFlowSelector selector;
    private readonly string operationKey;
    private readonly VersionChoice? choice;
    internal VersionFlowOperation(IServiceProvider provider, VersionFlowSelector selector, string operationKey, VersionChoice? choice)
    { selector.ValidateOperation(operationKey); this.provider = provider; this.selector = selector; this.operationKey = operationKey; this.choice = choice; }
    public VersionFlowOperation UseVersion(int version) => new(provider, selector, operationKey, VersionChoice.FromNumber(version));
    public VersionFlowOperation UseLatest() => new(provider, selector, operationKey, VersionChoice.Latest);
    public VersionFlowMatch<T> Match<T>() => new(provider, selector, operationKey, choice, ImmutableDictionary<int, Func<IServiceProvider, T>>.Empty);
    public VersionFlowAsyncMatch<T> MatchAsync<T>() => new(provider, selector, operationKey, choice, ImmutableDictionary<int, Func<IServiceProvider, CancellationToken, Task<T>>>.Empty);
}
public sealed class VersionFlowMatch<T>
{
    private readonly IServiceProvider provider;
    private readonly VersionFlowSelector selector;
    private readonly string operation;
    private readonly VersionChoice? choice;
    private readonly ImmutableDictionary<int, Func<IServiceProvider, T>> branches;
    internal VersionFlowMatch(IServiceProvider provider, VersionFlowSelector selector, string operation, VersionChoice? choice, ImmutableDictionary<int, Func<IServiceProvider, T>> branches)
    { this.provider = provider; this.selector = selector; this.operation = operation; this.choice = choice; this.branches = branches; }
    public VersionFlowMatch<T> Version<TService>(int version, Func<TService, T> callback) where TService : class
    {
        VersionChoice.FromNumber(version); ArgumentNullException.ThrowIfNull(callback);
        if (branches.ContainsKey(version)) throw new ArgumentException($"Duplicate callback version {version}.", nameof(version));
        return new(provider, selector, operation, choice, branches.Add(version, p => callback(p.GetRequiredService<VersionFlowDispatcherContext>().GetRequired<TService>())));
    }
    public T Execute()
    {
        var (key, method) = ConfigurationParser.Split(operation);
        var selected = selector.SelectBranches(key, method, branches.Keys, choice);
        return branches[selected.Number](provider);
    }
}
public sealed class VersionFlowAsyncMatch<T>
{
    private readonly IServiceProvider provider;
    private readonly VersionFlowSelector selector;
    private readonly string operation;
    private readonly VersionChoice? choice;
    private readonly ImmutableDictionary<int, Func<IServiceProvider, CancellationToken, Task<T>>> branches;
    internal VersionFlowAsyncMatch(IServiceProvider provider, VersionFlowSelector selector, string operation, VersionChoice? choice, ImmutableDictionary<int, Func<IServiceProvider, CancellationToken, Task<T>>> branches)
    { this.provider = provider; this.selector = selector; this.operation = operation; this.choice = choice; this.branches = branches; }
    public VersionFlowAsyncMatch<T> Version<TService>(int version, Func<TService, Task<T>> callback) where TService : class
    { ArgumentNullException.ThrowIfNull(callback); return Version<TService>(version, (service, _) => callback(service)); }
    public VersionFlowAsyncMatch<T> Version<TService>(int version, Func<TService, CancellationToken, Task<T>> callback) where TService : class
    {
        VersionChoice.FromNumber(version); ArgumentNullException.ThrowIfNull(callback);
        if (branches.ContainsKey(version)) throw new ArgumentException($"Duplicate callback version {version}.", nameof(version));
        return new(provider, selector, operation, choice, branches.Add(version, (p, ct) => callback(p.GetRequiredService<VersionFlowDispatcherContext>().GetRequired<TService>(), ct)));
    }
    public Task<T> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var (key, method) = ConfigurationParser.Split(operation);
        var selected = selector.SelectBranches(key, method, branches.Keys, choice);
        return branches[selected.Number](provider, cancellationToken);
    }
}


