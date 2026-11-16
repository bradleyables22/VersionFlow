using System.Collections.Immutable;
using Microsoft.Extensions.DependencyInjection;
namespace VersionFlow;

internal sealed class VersionFlowRuntime(IServiceProvider provider, VersionFlowCatalog catalog, VersionFlowSelector selector, SnapshotStore store) : IVersionFlow
{
    public bool FlagIsEnabled(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return store.Current.Flags.TryGetValue(name, out var enabled) && enabled;
    }
    public bool FlagIsDisabled(string name) => !FlagIsEnabled(name);
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
/// <summary>An immutable scoped operation builder. Add branches with Match or MatchAsync, then execute.</summary>
public sealed class VersionFlowOperation : IVersionFlowOperation
{
    private readonly IServiceProvider provider;
    private readonly VersionFlowSelector selector;
    private readonly string operationKey;
    private readonly VersionChoice? choice;
    internal VersionFlowOperation(IServiceProvider provider, VersionFlowSelector selector, string operationKey, VersionChoice? choice)
    { selector.ValidateOperation(operationKey); this.provider = provider; this.selector = selector; this.operationKey = operationKey; this.choice = choice; }
    /// <summary>Forces a version, overriding method, class, and default configuration.</summary>
    /// <param name="version">A positive version number that must have a branch at execution.</param>
    /// <returns>A new builder; the original remains unchanged.</returns>
    public VersionFlowOperation UseVersion(int version) => new(provider, selector, operationKey, VersionChoice.FromNumber(version));
    /// <summary>Selects the highest supplied branch version, overriding configuration.</summary>
    /// <returns>A new builder; the original remains unchanged.</returns>
    public VersionFlowOperation UseLatest() => new(provider, selector, operationKey, VersionChoice.Latest);
    /// <summary>Starts synchronous result-producing branches for this operation.</summary>
    /// <typeparam name="T">The common result type for all branches.</typeparam>
    /// <returns>An empty branch builder. Add Version callbacks before Execute.</returns>
    public VersionFlowMatch<T> Match<T>() => new(provider, selector, operationKey, choice, ImmutableDictionary<int, Func<IServiceProvider, T>>.Empty);
    /// <summary>Starts asynchronous result-producing branches for this operation.</summary>
    /// <typeparam name="T">The common result type for all branches.</typeparam>
    /// <returns>An empty branch builder. Add Version callbacks before ExecuteAsync.</returns>
    public VersionFlowAsyncMatch<T> MatchAsync<T>() => new(provider, selector, operationKey, choice, ImmutableDictionary<int, Func<IServiceProvider, CancellationToken, Task<T>>>.Empty);
}
/// <summary>An immutable set of synchronous DI-service callbacks. Only the selected callback is executed.</summary>
/// <typeparam name="T">The common result type produced by all branches.</typeparam>
public sealed class VersionFlowMatch<T>
{
    private readonly IServiceProvider provider;
    private readonly VersionFlowSelector selector;
    private readonly string operation;
    private readonly VersionChoice? choice;
    private readonly ImmutableDictionary<int, Func<IServiceProvider, T>> branches;
    internal VersionFlowMatch(IServiceProvider provider, VersionFlowSelector selector, string operation, VersionChoice? choice, ImmutableDictionary<int, Func<IServiceProvider, T>> branches)
    { this.provider = provider; this.selector = selector; this.operation = operation; this.choice = choice; this.branches = branches; }
    /// <summary>Adds a version branch without resolving or invoking its service.</summary>
    /// <typeparam name="TService">The service type registered in DI for this branch.</typeparam>
    /// <param name="version">A unique positive branch number.</param>
    /// <param name="callback">Invoked only if selected. Adapt the service response to the common result type; callback exceptions propagate.</param>
    /// <returns>A new builder containing this branch; the original is unchanged.</returns>
    public VersionFlowMatch<T> Version<TService>(int version, Func<TService, T> callback) where TService : class
    {
        VersionChoice.FromNumber(version); ArgumentNullException.ThrowIfNull(callback);
        if (branches.ContainsKey(version)) throw new ArgumentException($"Duplicate callback version {version}.", nameof(version));
        return new(provider, selector, operation, choice, branches.Add(version, p => callback(p.GetRequiredService<VersionFlowDispatcherContext>().GetRequired<TService>())));
    }
    /// <summary>Selects a branch using current configuration, resolves its service, and executes it once.</summary>
    /// <returns>The selected callback's result.</returns>
    /// <exception cref="VersionFlowSelectionException">No branch matches the selected version.</exception>
    /// <remarks>Only the chosen service is resolved. Callback exceptions propagate.</remarks>
    public T Execute()
    {
        var (key, method) = ConfigurationParser.Split(operation);
        var selected = selector.SelectBranches(key, method, branches.Keys, choice);
        return branches[selected.Number](provider);
    }
}
/// <summary>An immutable set of asynchronous DI-service callbacks. Only the selected callback is executed.</summary>
/// <typeparam name="T">The common result type produced by all branches.</typeparam>
public sealed class VersionFlowAsyncMatch<T>
{
    private readonly IServiceProvider provider;
    private readonly VersionFlowSelector selector;
    private readonly string operation;
    private readonly VersionChoice? choice;
    private readonly ImmutableDictionary<int, Func<IServiceProvider, CancellationToken, Task<T>>> branches;
    internal VersionFlowAsyncMatch(IServiceProvider provider, VersionFlowSelector selector, string operation, VersionChoice? choice, ImmutableDictionary<int, Func<IServiceProvider, CancellationToken, Task<T>>> branches)
    { this.provider = provider; this.selector = selector; this.operation = operation; this.choice = choice; this.branches = branches; }
    /// <summary>Adds a version branch without resolving or invoking its service.</summary>
    /// <typeparam name="TService">The service type registered in DI for this branch.</typeparam>
    /// <param name="version">A unique positive branch number.</param>
    /// <param name="callback">Invoked only if selected. Adapt the service response to the common result type; callback exceptions propagate.</param>
    /// <returns>A new builder containing this branch; the original is unchanged.</returns>
    public VersionFlowAsyncMatch<T> Version<TService>(int version, Func<TService, Task<T>> callback) where TService : class
    { ArgumentNullException.ThrowIfNull(callback); return Version<TService>(version, (service, _) => callback(service)); }
    /// <summary>Adds a version branch without resolving or invoking its service.</summary>
    /// <typeparam name="TService">The service type registered in DI for this branch.</typeparam>
    /// <param name="version">A unique positive branch number.</param>
    /// <param name="callback">Invoked only if selected. Adapt the service response to the common result type; callback exceptions propagate.</param>
    /// <returns>A new builder containing this branch; the original is unchanged.</returns>
    public VersionFlowAsyncMatch<T> Version<TService>(int version, Func<TService, CancellationToken, Task<T>> callback) where TService : class
    {
        VersionChoice.FromNumber(version); ArgumentNullException.ThrowIfNull(callback);
        if (branches.ContainsKey(version)) throw new ArgumentException($"Duplicate callback version {version}.", nameof(version));
        return new(provider, selector, operation, choice, branches.Add(version, (p, ct) => callback(p.GetRequiredService<VersionFlowDispatcherContext>().GetRequired<TService>(), ct)));
    }
    /// <summary>Selects and executes one asynchronous branch using current configuration.</summary>
    /// <param name="cancellationToken">Checked before selection and forwarded to token-aware callbacks; callbacks without a token must manage their own cancellation.</param>
    /// <returns>The selected callback's task. Failures and cancellation propagate.</returns>
    public Task<T> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var (key, method) = ConfigurationParser.Split(operation);
        var selected = selector.SelectBranches(key, method, branches.Keys, choice);
        return branches[selected.Number](provider, cancellationToken);
    }
}


