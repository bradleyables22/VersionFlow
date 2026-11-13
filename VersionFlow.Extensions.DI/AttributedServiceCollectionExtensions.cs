using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;

namespace VersionFlow.Extensions.DI;

public static class AttributedServiceCollectionExtensions
{
    private const string ScanWarning = "Assembly scanning requires preserving attributed classes, their attributes, interfaces, and public constructors when trimming.";

    /// <summary>Scans the immediate calling assembly for ServiceAttribute and derived attributes.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    [RequiresUnreferencedCode(ScanWarning)]
    public static IServiceCollection AddAttributedServices(this IServiceCollection services) =>
        AddAttributedServices(services, Assembly.GetCallingAssembly());

    /// <summary>Scans only the supplied assemblies. Concrete registrations are always included.</summary>
    [RequiresUnreferencedCode(ScanWarning)]
    public static IServiceCollection AddAttributedServices(this IServiceCollection services, params Assembly[] assemblies) =>
        AddAttributedServices<ServiceAttribute>(services, assemblies);

    /// <summary>Scans an assembly identified by a marker type.</summary>
    [RequiresUnreferencedCode(ScanWarning)]
    public static IServiceCollection AddAttributedServicesFromAssemblyContaining<TMarker>(this IServiceCollection services) =>
        AddAttributedServices(services, typeof(TMarker).Assembly);

    /// <summary>Registers only classes marked by the specified ServiceAttribute subtype.</summary>
    [RequiresUnreferencedCode(ScanWarning)]
    public static IServiceCollection AddAttributedServices<TAttribute>(this IServiceCollection services, params Assembly[] assemblies)
        where TAttribute : ServiceAttribute
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(assemblies);
        if (assemblies.Length == 0) throw new ArgumentException("Supply at least one assembly.", nameof(assemblies));
        if (assemblies.Any(a => a is null)) throw new ArgumentException("Assemblies cannot contain null.", nameof(assemblies));

        // Stage everything first: invalid declarations/conflicts never leave a partially changed collection.
        var staged = new ServiceCollection();
        foreach (var descriptor in services) ((IServiceCollection)staged).Add(descriptor);
        foreach (var assembly in assemblies.Distinct().OrderBy(a => a.FullName, StringComparer.Ordinal))
        {
            Type[] types;
            try { types = assembly.GetTypes(); }
            catch (ReflectionTypeLoadException e)
            {
                throw new InvalidOperationException($"Cannot scan assembly '{assembly.FullName}'. Resolve its missing dependencies before registering services. {string.Join("; ", e.LoaderExceptions.Where(x => x is not null).Select(x => x!.Message))}", e);
            }
            foreach (var type in types.OrderBy(t => t.FullName, StringComparer.Ordinal))
            {
                var attributes = type.GetCustomAttributes<ServiceAttribute>(inherit: false).ToArray();
                if (!attributes.Any(a => a is TAttribute)) continue;
                if (attributes.Length != 1) throw new InvalidOperationException($"'{type}' has multiple service registration attributes. Use one lifetime declaration.");
                var attribute = attributes[0];
                if (!type.IsClass || type.IsAbstract || type.ContainsGenericParameters || type.GetConstructors().Length == 0)
                    throw new InvalidOperationException($"Attributed service '{type}' must be a closed concrete class with a public constructor.");
                var contracts = (attribute.GetServiceTypes(type)
                    ?? throw new InvalidOperationException($"Service attribute on '{type}' returned a null contract collection.")).Distinct().ToArray();
                foreach (var contract in contracts)
                    if (contract is null || contract.ContainsGenericParameters || !contract.IsAssignableFrom(type))
                        throw new InvalidOperationException($"Service contract '{contract}' is not assignable from '{type}'.");
                Register(staged, type, type, attribute.Lifetime);
                foreach (var contract in contracts.Where(c => c != type)) Register(staged, contract, type, attribute.Lifetime);
            }
        }
        foreach (var descriptor in staged.Skip(services.Count)) services.Add(descriptor);
        return services;
    }

    private static void Register(IServiceCollection services, Type contract, Type implementation, ServiceLifetime lifetime)
    {
        var existing = services.Where(d => !d.IsKeyedService && d.ServiceType == contract).ToArray();
        if (existing.Length == 1 && existing[0].ImplementationType == implementation && existing[0].Lifetime == lifetime)
            return;
        if (existing.Length > 0)
            throw new InvalidOperationException($"Service '{contract}' already has a conflicting registration. Attribute registration for '{implementation}' ({lifetime}) will not replace it.");

        // Every binding owns its instance, exactly like AddScoped<IContract, Implementation>().
        // Forwarding factories would make DI capture the same disposable object more than once.
        services.Add(ServiceDescriptor.Describe(contract, implementation, lifetime));
    }
}
