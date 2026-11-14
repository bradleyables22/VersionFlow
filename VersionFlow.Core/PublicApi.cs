namespace VersionFlow;

/// <summary>Declares a class version, an operations host, or an independent method version.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class VersionedAttribute : Attribute
{
    /// <summary>Declares a class implementation version of a contract.</summary>
    public VersionedAttribute(Type contractType, int version)
    {
        ContractType = contractType;
        Version = version;
    }

    /// <summary>Declares a host whose methods have independent versions.</summary>
    public VersionedAttribute(Type contractType) => ContractType = contractType;

    /// <summary>Declares a version of an operation on an operations host.</summary>
    public VersionedAttribute(string operation, int version)
    {
        Operation = operation;
        Version = version;
    }

    public Type? ContractType { get; }
    public string? Operation { get; }
    public int? Version { get; }
    /// <summary>Optional logical key for a class version or operations host; not valid on methods.</summary>
    public string? Key { get; set; }
}

public readonly record struct VersionChoice
{
    public int? Number { get; }
    public bool IsLatest => Number is null;
    private VersionChoice(int? number) => Number = number;
    public static VersionChoice Latest => new(null);
    public static VersionChoice FromNumber(int number) => number > 0 ? new(number) : throw new ArgumentOutOfRangeException(nameof(number));
    public static VersionChoice Parse(string value) => value == "Latest" ? Latest
        : int.TryParse(value, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var n) && n > 0
            ? FromNumber(n) : throw new VersionFlowConfigurationException($"Invalid version '{value}'; expected Latest or a positive integer.");
    public override string ToString() => Number?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "Latest";
}
public enum VersionSelectionSource { Global, Class, Method, Explicit }
public sealed record VersionSelection(string Key, string? Method, int Number, VersionSelectionSource Source,
    System.Collections.Immutable.ImmutableArray<int> AvailableVersions, long SnapshotIdentity);
public class VersionFlowConfigurationException(string message) : Exception(message);
public class VersionFlowSelectionException(string message) : Exception(message);
public class VersionFlowRegistrationException(string message, Exception? inner = null) : Exception(message, inner);

public interface IVersionFlow
{
    /// <summary>Returns the current value of a case-sensitive named flag, or false if it is missing.</summary>
    bool IsEnabled(string name);
    IVersionFlowContract<T> For<T>() where T : class;
    VersionFlowOperation For(string operationKey);
    VersionSelection Select(string key, string? method = null);
}
public interface IVersionFlowContract<T> where T : class
{
    T Resolve();
    IVersionFlowContract<T> UseVersion(int version);
    IVersionFlowContract<T> UseLatest();
    VersionSelection Selection { get; }
}

