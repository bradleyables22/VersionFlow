using Microsoft.Extensions.DependencyInjection;

namespace VersionFlow.Extensions.DI;

/// <summary>Registers a concrete class and optional service contracts, each with its own DI-managed lifetime and instance.</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public class ServiceAttribute : Attribute
{
    public ServiceAttribute(ServiceLifetime lifetime = ServiceLifetime.Scoped, params Type[] serviceTypes)
    {
        if (!Enum.IsDefined(lifetime)) throw new ArgumentOutOfRangeException(nameof(lifetime));
        ArgumentNullException.ThrowIfNull(serviceTypes);
        if (serviceTypes.Any(t => t is null)) throw new ArgumentException("Service contracts cannot contain null.", nameof(serviceTypes));
        Lifetime = lifetime;
        ServiceTypes = Array.AsReadOnly((Type[])serviceTypes.Clone());
    }

    /// <summary>Binds an interface/base contract with scoped lifetime unless another lifetime is specified.</summary>
    public ServiceAttribute(Type serviceType, ServiceLifetime lifetime = ServiceLifetime.Scoped, params Type[] additionalServiceTypes)
        : this(lifetime, Combine(serviceType, additionalServiceTypes)) { }

    public ServiceLifetime Lifetime { get; }
    public IReadOnlyList<Type> ServiceTypes { get; }

    /// <summary>Also binds implemented interfaces, excluding IDisposable and IAsyncDisposable. Defaults to false.</summary>
    public bool AsImplementedInterfaces { get; set; }

    /// <summary>Specialized attributes can select their own service contracts.</summary>
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

