using Microsoft.Extensions.DependencyInjection;

namespace VersionFlow.Extensions.DI;

/// <summary>Registers a concrete class and optional service contracts, each with its own DI-managed lifetime and instance.</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public class ServiceAttribute : Attribute
{
    /// <summary>Marks a concrete type for attribute scanning, optionally adding separate contract bindings.</summary>
    /// <param name="lifetime">The lifetime for each binding; scoped by default.</param>
    /// <param name="serviceTypes">Optional assignable interfaces or base types. Leave empty for VersionFlow implementations.</param>
    public ServiceAttribute(ServiceLifetime lifetime = ServiceLifetime.Scoped, params Type[] serviceTypes)
    {
        if (!Enum.IsDefined(lifetime)) throw new ArgumentOutOfRangeException(nameof(lifetime));
        ArgumentNullException.ThrowIfNull(serviceTypes);
        if (serviceTypes.Any(t => t is null)) throw new ArgumentException("Service contracts cannot contain null.", nameof(serviceTypes));
        Lifetime = lifetime;
        ServiceTypes = Array.AsReadOnly((Type[])serviceTypes.Clone());
    }

    /// <summary>Binds an interface/base contract with scoped lifetime unless another lifetime is specified.</summary>
    /// <param name="serviceType">An assignable interface or base type to bind in addition to the concrete class.</param>
    /// <param name="lifetime">The lifetime of each independent DI binding; scoped by default.</param>
    /// <param name="additionalServiceTypes">Additional assignable contracts; each binding owns a separate DI-managed instance.</param>
    public ServiceAttribute(Type serviceType, ServiceLifetime lifetime = ServiceLifetime.Scoped, params Type[] additionalServiceTypes)
        : this(lifetime, Combine(serviceType, additionalServiceTypes)) { }

    /// <summary>The DI lifetime used independently for each concrete or contract binding.</summary>
    public ServiceLifetime Lifetime { get; }
    /// <summary>A read-only snapshot of the explicitly requested contracts.</summary>
    public IReadOnlyList<Type> ServiceTypes { get; }

    /// <summary>Also binds implemented interfaces, excluding IDisposable and IAsyncDisposable. Defaults to false.</summary>
    public bool AsImplementedInterfaces { get; set; }

    /// <summary>Specialized attributes can select their own service contracts.</summary>
    /// <param name="implementationType">The concrete class being registered.</param>
    /// <returns>The requested contract types; the scanner always registers the concrete type separately.</returns>
    public virtual IEnumerable<Type> GetServiceTypes(Type implementationType)
    {
        ArgumentNullException.ThrowIfNull(implementationType);
        return AsImplementedInterfaces
            ? ServiceTypes.Concat(implementationType.GetInterfaces().Where(t => t != typeof(IDisposable) && t != typeof(IAsyncDisposable)))
            : ServiceTypes;
    }

    private static Type[] Combine(Type serviceType, Type[] additionalServiceTypes)
    {
        ArgumentNullException.ThrowIfNull(serviceType);
        ArgumentNullException.ThrowIfNull(additionalServiceTypes);
        return [serviceType, .. additionalServiceTypes];
    }
}

