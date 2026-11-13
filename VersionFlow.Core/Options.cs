using System.Collections.Immutable;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace VersionFlow;

public sealed class VersionFlowOptions
{
    public string Default { get; set; } = "Latest";
    public Dictionary<string, string> Classes { get; set; } = new(StringComparer.Ordinal);
    public Dictionary<string, string> Methods { get; set; } = new(StringComparer.Ordinal);
    public bool ValidateOnStart { get; set; } = true;
    public string MissingVersionBehavior { get; set; } = "Throw";
}
internal sealed record ConfigurationSnapshot(long Identity, VersionChoice Default,
    ImmutableDictionary<string, VersionChoice> Classes, ImmutableDictionary<string, VersionChoice> Methods);

internal static class ConfigurationParser
{
    public static ConfigurationSnapshot Parse(VersionFlowOptions options, VersionFlowCatalog catalog, long identity)
    {
        if (options.MissingVersionBehavior != "Throw") throw new VersionFlowConfigurationException("Only MissingVersionBehavior=Throw is supported.");
        var snapshot = new ConfigurationSnapshot(identity, VersionChoice.Parse(options.Default),
            (options.Classes ?? throw new VersionFlowConfigurationException("Classes cannot be null.")).ToImmutableDictionary(x => x.Key, x => VersionChoice.Parse(x.Value), StringComparer.Ordinal),
            (options.Methods ?? throw new VersionFlowConfigurationException("Methods cannot be null.")).ToImmutableDictionary(x => x.Key, x => VersionChoice.Parse(x.Value), StringComparer.Ordinal));
        foreach (var key in snapshot.Classes.Keys)
            if (!catalog.Contracts.ContainsKey(key) && !catalog.FluentOperations.Any(o => Split(o).Key == key))
                throw new VersionFlowConfigurationException($"Unknown class key '{key}'. Link its generated catalog or declare its fluent operation.");
        foreach (var key in snapshot.Methods.Keys)
            if (!catalog.FluentOperations.Contains(key) && !catalog.Contracts.Values.Any(c => c.Methods.Keys.Any(m => c.Key + "." + m == key)))
                throw new VersionFlowConfigurationException($"Unknown method key '{key}'.");
        foreach (var contract in catalog.Contracts.Values)
            foreach (var method in contract.Methods)
                try { VersionFlowSelector.Choose(snapshot, contract.Key, method.Key, method.Value, null); }
                catch (VersionFlowSelectionException e) { throw new VersionFlowConfigurationException(e.Message); }
        return snapshot;
    }
    internal static (string Key, string Method) Split(string operation)
    {
        var index = operation.LastIndexOf('.');
        if (index <= 0 || index == operation.Length - 1) throw new ArgumentException("Expected a qualified operation key such as Orders.Process.", nameof(operation));
        return (operation[..index], operation[(index + 1)..]);
    }
}
internal sealed class VersionFlowOptionsValidator(VersionFlowCatalog catalog) : IValidateOptions<VersionFlowOptions>
{
    public ValidateOptionsResult Validate(string? name, VersionFlowOptions options)
    {
        try { ConfigurationParser.Parse(options, catalog, 0); return ValidateOptionsResult.Success; }
        catch (VersionFlowConfigurationException e) { return ValidateOptionsResult.Fail(e.Message); }
    }
}

// Subscribe at the options token layer: IOptionsMonitor does not invoke OnChange when validation fails.
// Creating candidates through IOptionsFactory preserves all Configure/PostConfigure/Validate registrations.
internal sealed class SnapshotStore : IDisposable
{
    private readonly object gate = new();
    private readonly IOptionsFactory<VersionFlowOptions> factory;
    private readonly VersionFlowCatalog catalog;
    private readonly ILogger<SnapshotStore> logger;
    private readonly List<IDisposable> subscriptions = [];
    private ConfigurationSnapshot current;
    public ConfigurationSnapshot Current => Volatile.Read(ref current);
    public SnapshotStore(IOptionsFactory<VersionFlowOptions> factory, VersionFlowCatalog catalog,
        IEnumerable<IOptionsChangeTokenSource<VersionFlowOptions>> sources, ILogger<SnapshotStore> logger)
    {
        this.factory = factory; this.catalog = catalog; this.logger = logger;
        current = Create(1);
        foreach (var source in sources.Where(s => string.IsNullOrEmpty(s.Name)))
            subscriptions.Add(ChangeToken.OnChange(source.GetChangeToken, Reload));
        // Close the window between initial reading and subscription.
        Reload();
    }
    private ConfigurationSnapshot Create(long id)
    {
        try { return ConfigurationParser.Parse(factory.Create(Options.DefaultName), catalog, id); }
        catch (OptionsValidationException e) { throw new VersionFlowConfigurationException(e.Message); }
    }
    private void Reload()
    {
        lock (gate)
        {
            try { Volatile.Write(ref current, Create(current.Identity + 1)); }
            catch (Exception e) { logger.LogError(e, "VersionFlow configuration update rejected; retaining snapshot {SnapshotIdentity}.", current.Identity); }
        }
    }
    public void Dispose() { foreach (var subscription in subscriptions) subscription.Dispose(); }
}
public sealed class VersionFlowSelector
{
    private readonly SnapshotStore store;
    private readonly VersionFlowCatalog catalog;
    internal VersionFlowSelector(SnapshotStore store, VersionFlowCatalog catalog) { this.store = store; this.catalog = catalog; }
    public VersionSelection Select(string key, string? method = null, VersionChoice? choice = null)
    {
        if (!catalog.Contracts.TryGetValue(key, out var contract)) throw new VersionFlowSelectionException($"Unknown catalog key '{key}'.");
        if (method is null && contract.IsOperationsHost)
            throw new VersionFlowSelectionException($"Operations host '{key}' has independent version sets; specify a method for introspection.");
        var available = method is null ? contract.Versions : contract.Methods.TryGetValue(method, out var versions) ? versions
            : throw new VersionFlowSelectionException($"Unknown method '{key}.{method}'.");
        return Choose(store.Current, key, method, available, choice);
    }
    internal void ValidateOperation(string operationKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationKey);
        var (key, method) = ConfigurationParser.Split(operationKey);
        if (!catalog.FluentOperations.Contains(operationKey) &&
            !(catalog.Contracts.TryGetValue(key, out var contract) && contract.Methods.ContainsKey(method)))
            throw new VersionFlowSelectionException($"Unknown operation '{operationKey}'. Link its generated catalog or declare the operation with AddVersionFlowOperation(\"{operationKey}\"). Operation keys are case-sensitive.");
    }
    internal VersionSelection SelectBranches(string key, string method, IEnumerable<int> available, VersionChoice? choice) =>
        Choose(store.Current, key, method, available.Order().ToImmutableArray(), choice);
    internal static VersionSelection Choose(ConfigurationSnapshot snapshot, string key, string? method, ImmutableArray<int> available, VersionChoice? choice)
    {
        var source = VersionSelectionSource.Global;
        var selected = snapshot.Default;
        if (snapshot.Classes.TryGetValue(key, out var classChoice)) { selected = classChoice; source = VersionSelectionSource.Class; }
        if (method is not null && snapshot.Methods.TryGetValue(key + "." + method, out var methodChoice)) { selected = methodChoice; source = VersionSelectionSource.Method; }
        if (choice is { } explicitChoice) { selected = explicitChoice; source = VersionSelectionSource.Explicit; }
        var number = selected.Number ?? (available.IsEmpty ? 0 : available.Max());
        if (!available.Contains(number)) throw new VersionFlowSelectionException($"Requested '{selected}' for '{key}' method '{method ?? "<class>"}' is unavailable. Available versions: [{string.Join(", ", available)}].");
        return new(key, method, number, source, available, snapshot.Identity);
    }
}

