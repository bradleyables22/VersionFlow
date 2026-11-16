namespace VersionFlow;

/// <summary>Declares a class version, an operations host, or an independent method version.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class VersionedAttribute : Attribute
{
    /// <summary>Declares a class implementation version of a contract.</summary>
    /// <param name="contractType">The non-generic contract interface. Register the implementation or host by concrete type in DI.</param>
    /// <param name="version">A positive version number; availability is checked when the operation is selected.</param>
    public VersionedAttribute(Type contractType, int version)
    {
        ContractType = contractType;
        Version = version;
    }

    /// <summary>Declares a host whose methods have independent versions.</summary>
    /// <param name="contractType">The non-generic contract interface. Register the implementation or host by concrete type in DI.</param>
    public VersionedAttribute(Type contractType) => ContractType = contractType;

    /// <summary>Declares a version of an operation on an operations host.</summary>
    /// <param name="operation">The exact short contract method name, for example nameof(IContract.Calculate).</param>
    /// <param name="version">A positive version number; availability is checked when the operation is selected.</param>
    public VersionedAttribute(string operation, int version)
    {
        Operation = operation;
        Version = version;
    }

    /// <summary>The non-generic interface to route, or null for a method annotation.</summary>
    public Type? ContractType { get; }
    /// <summary>The contract method name for a method annotation; null for a class annotation.</summary>
    public string? Operation { get; }
    /// <summary>The positive version number; null marks an operations host.</summary>
    public int? Version { get; }
    /// <summary>Optional logical key for a class version or operations host; not valid on methods.</summary>
    public string? Key { get; set; }
}

/// <summary>Selects a positive version number or the highest available version.</summary>
public readonly record struct VersionChoice
{
    /// <summary>The requested number, or null when selecting Latest.</summary>
    public int? Number { get; }
    /// <summary>Whether this choice selects the highest available version.</summary>
    public bool IsLatest => Number is null;
    private VersionChoice(int? number) => Number = number;
    /// <summary>Selects the highest version available for the operation at execution time.</summary>
    public static VersionChoice Latest => new(null);
    /// <summary>Creates an explicit version choice.</summary>
    /// <param name="number">A positive version number.</param>
    /// <returns>The explicit choice; availability is checked when selecting an operation.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The number is not positive.</exception>
    public static VersionChoice FromNumber(int number) => number > 0 ? new(number) : throw new ArgumentOutOfRangeException(nameof(number));
    /// <summary>Parses the exact string Latest or a positive integer without whitespace or a sign.</summary>
    /// <param name="value">The configuration value to parse.</param>
    /// <returns>The parsed choice.</returns>
    /// <exception cref="VersionFlowConfigurationException">The value is not a supported version choice.</exception>
    public static VersionChoice Parse(string value) => value == "Latest" ? Latest
        : int.TryParse(value, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var n) && n > 0
            ? FromNumber(n) : throw new VersionFlowConfigurationException($"Invalid version '{value}'; expected Latest or a positive integer.");
    /// <summary>Formats the choice as Latest or an invariant positive integer.</summary>
    /// <returns>The value accepted by version configuration.</returns>
    public override string ToString() => Number?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "Latest";
}
/// <summary>Identifies the setting that won version-selection precedence.</summary>
public enum VersionSelectionSource
{
    /// <summary>The global Default setting selected the version.</summary>
    Global,
    /// <summary>A class override selected the version.</summary>
    Class,
    /// <summary>A method override selected the version.</summary>
    Method,
    /// <summary>A caller supplied UseVersion or UseLatest.</summary>
    Explicit
}
/// <summary>A selection snapshot for inspection; querying it does not execute an implementation or pin a later call.</summary>
/// <param name="Key">The case-sensitive contract key.</param>
/// <param name="Method">The short method name, or null for class-level inspection.</param>
/// <param name="Number">The selected available version.</param>
/// <param name="Source">The winning configuration level or explicit choice.</param>
/// <param name="AvailableVersions">The versions considered for this selection.</param>
/// <param name="SnapshotIdentity">The configuration snapshot identity, useful when comparing observations.</param>
public sealed record VersionSelection(string Key, string? Method, int Number, VersionSelectionSource Source,
    System.Collections.Immutable.ImmutableArray<int> AvailableVersions, long SnapshotIdentity);
/// <summary>Reports invalid version or flag configuration. Correct the settings before retrying.</summary>
/// <param name="message">Details identifying the invalid configuration.</param>
public class VersionFlowConfigurationException(string message) : Exception(message);
/// <summary>Reports an unknown operation or unavailable version. Check the key and available versions.</summary>
/// <param name="message">Details of the failed selection.</param>
public class VersionFlowSelectionException(string message) : Exception(message);
/// <summary>Reports missing or conflicting DI/catalog registrations.</summary>
/// <param name="message">Details of the registration that must be corrected.</param>
/// <param name="inner">The underlying failure, if any.</param>
public class VersionFlowRegistrationException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>Checks flags and selects versioned services within the current DI scope.</summary>
/// <remarks>Register with AddVersionFlow and resolve within a scope. Do not capture this service in a singleton.</remarks>
public interface IVersionFlow
{
    /// <summary>Returns the current value of a case-sensitive named flag, or false if it is missing.</summary>
    /// <param name="name">A case-sensitive flag name.</param>
    /// <returns>True only when the current valid configuration contains the flag with value true.</returns>
    /// <exception cref="ArgumentNullException">The name is null.</exception>
    bool FlagIsEnabled(string name);
    /// <summary>Returns true when a case-sensitive named flag is false or missing.</summary>
    /// <param name="name">A case-sensitive flag name.</param>
    /// <returns>True when the flag is false or absent from the current valid configuration.</returns>
    /// <exception cref="ArgumentNullException">The name is null.</exception>
    bool FlagIsDisabled(string name);
    /// <summary>Starts typed selection for a generated contract; use Resolve to obtain a routed facade.</summary>
    /// <typeparam name="T">The non-generic contract interface declared by Versioned.</typeparam>
    /// <returns>A scoped builder that follows configuration unless an explicit choice is applied.</returns>
    /// <exception cref="VersionFlowRegistrationException">The contract has no linked catalog.</exception>
    IVersionFlowContract<T> For<T>() where T : class;
    /// <summary>Starts fluent branching for a known operation, such as Pricing.Calculate.</summary>
    /// <param name="operationKey">A case-sensitive qualified name from generated metadata or AddVersionFlowOperation.</param>
    /// <returns>A builder to which result-producing service branches can be added.</returns>
    /// <remarks>This does not resolve services. Declare fluent-only operation names during startup.</remarks>
    VersionFlowOperation For(string operationKey);
    /// <summary>Inspects the version selected by current configuration without calling the implementation.</summary>
    /// <param name="key">The case-sensitive contract key, not the qualified operation name.</param>
    /// <param name="method">The short method name. Required for independently versioned operations hosts.</param>
    /// <returns>Selection details from one configuration snapshot.</returns>
    /// <exception cref="VersionFlowSelectionException">The key, method, or selected version is unavailable.</exception>
    VersionSelection Select(string key, string? method = null);
}
/// <summary>An immutable typed selection builder tied to the scope that created it.</summary>
/// <typeparam name="T">The generated contract interface.</typeparam>
public interface IVersionFlowContract<T> where T : class
{
    /// <summary>Creates a routed facade. Call its interface methods to resolve and invoke the selected concrete service.</summary>
    /// <returns>A facade, not the concrete implementation. Without an explicit choice, each call follows current configuration.</returns>
    T Resolve();
    /// <summary>Returns a new builder that forces a version for every call through its facade.</summary>
    /// <param name="version">A positive version number; it must exist for each invoked operation.</param>
    /// <returns>A new builder; the original is unchanged.</returns>
    IVersionFlowContract<T> UseVersion(int version);
    /// <summary>Returns a new builder that selects each operation's highest version, overriding configuration.</summary>
    /// <returns>A new builder; the original is unchanged.</returns>
    IVersionFlowContract<T> UseLatest();
    /// <summary>Inspects class-level selection with this builder's explicit choice, if any.</summary>
    /// <remarks>For an operations host, use IVersionFlow.Select with a method name instead.</remarks>
    VersionSelection Selection { get; }
}

