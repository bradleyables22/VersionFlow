using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace VersionFlow;

public static class VersionFlowServiceCollectionExtensions
{
    public static IServiceCollection AddVersionFlowCatalog<T>(this IServiceCollection services) where T : class, IVersionFlowCatalog, new()
    {
        if (!services.Any(d => d.ServiceType == typeof(IVersionFlowCatalog) && d.ImplementationInstance is T))
            services.AddSingleton<IVersionFlowCatalog>(new T());
        return services;
    }
    public static IServiceCollection AddVersionFlowOperation(this IServiceCollection services, string operationKey)
    {
        ConfigurationParser.Split(operationKey);
        if (!services.Any(d => d.ImplementationInstance is FluentOperationDeclaration o && o.Key == operationKey))
            services.AddSingleton(new FluentOperationDeclaration(operationKey));
        return services;
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static IServiceCollection AddVersionFlow(this IServiceCollection services, IConfiguration section,
        bool autoRegister = true, Assembly? assembly = null)
    {
        ArgumentNullException.ThrowIfNull(section);
        AddRuntimeOptions(services).Bind(section);
        if (autoRegister) AutoRegister(services, assembly ?? Assembly.GetCallingAssembly());
        return services;
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static IServiceCollection AddVersionFlow(this IServiceCollection services, Action<VersionFlowOptions> configure,
        bool autoRegister = true, Assembly? assembly = null)
    {
        ArgumentNullException.ThrowIfNull(configure);
        AddRuntimeOptions(services).Configure(configure);
        if (autoRegister) AutoRegister(services, assembly ?? Assembly.GetCallingAssembly());
        return services;
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static IServiceCollection AddVersionFlow(this IServiceCollection services,
        bool autoRegister = true, Assembly? assembly = null)
    {
        AddRuntimeOptions(services);
        if (autoRegister) AutoRegister(services, assembly ?? Assembly.GetCallingAssembly());
        return services;
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static OptionsBuilder<VersionFlowOptions> AddVersionFlowOptions(this IServiceCollection services,
        bool autoRegister = true, Assembly? assembly = null)
    {
        var options = AddRuntimeOptions(services);
        if (autoRegister) AutoRegister(services, assembly ?? Assembly.GetCallingAssembly());
        return options;
    }
    // Preserve the original method signatures for already-compiled consumers.
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static IServiceCollection AddVersionFlow(this IServiceCollection services, IConfiguration section) =>
        AddVersionFlow(services, section, autoRegister: true, assembly: Assembly.GetCallingAssembly());

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static IServiceCollection AddVersionFlow(this IServiceCollection services, Action<VersionFlowOptions> configure) =>
        AddVersionFlow(services, configure, autoRegister: true, assembly: Assembly.GetCallingAssembly());

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static OptionsBuilder<VersionFlowOptions> AddVersionFlowOptions(this IServiceCollection services) =>
        AddVersionFlowOptions(services, autoRegister: true, assembly: Assembly.GetCallingAssembly());
    private static void AutoRegister(IServiceCollection services, Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(services);
        // Explicit catalogs preserve the existing advanced setup's chosen scope.
        var discovery = services.FirstOrDefault(d => d.ServiceType == typeof(AutomaticCatalogRegistration))?.ImplementationInstance as AutomaticCatalogRegistration;
        if (discovery is not null || !services.Any(d => d.ServiceType == typeof(IVersionFlowCatalog)))
        {
            var metadata = assembly.GetCustomAttribute<GeneratedVersionFlowCatalogAttribute>();
            if (metadata is not null && (discovery is null || !discovery.CatalogTypes.Contains(metadata.CatalogType)))
            {
                var root = Activator.CreateInstance(metadata.CatalogType) as IVersionFlowCatalog
                    ?? throw new VersionFlowRegistrationException($"Invalid generated catalog for '{assembly.FullName}'.");
                if (discovery is null) { discovery = new AutomaticCatalogRegistration(); services.AddSingleton(discovery); }
                void Link(IVersionFlowCatalog catalog)
                {
                    if (!discovery.CatalogTypes.Add(catalog.GetType())) return;
                    services.AddSingleton(catalog);
                    foreach (var referenced in catalog.ReferencedCatalogs) Link(referenced);
                }
                Link(root);
            }
        }
        services.AddVersionFlowDispatchers();
    }
    private sealed class AutomaticCatalogRegistration
    {
        public HashSet<Type> CatalogTypes { get; } = [];
    }
    private static OptionsBuilder<VersionFlowOptions> AddRuntimeOptions(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddLogging();
        services.TryAddSingleton<VersionFlowCatalog>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<VersionFlowOptions>, VersionFlowOptionsValidator>());
        services.TryAddSingleton<SnapshotStore>();
        services.TryAddSingleton(p => new VersionFlowSelector(p.GetRequiredService<SnapshotStore>(), p.GetRequiredService<VersionFlowCatalog>()));
        services.TryAddScoped<VersionFlowDispatcherContext>();
        services.TryAddScoped<IVersionFlow, VersionFlowRuntime>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, VersionFlowStartupValidation>());
        return services.AddOptions<VersionFlowOptions>();
    }
    public static IServiceCollection AddVersionFlowDispatchers(this IServiceCollection services)
    {
        AddRuntimeOptions(services);
        var catalogs = services.Where(d => d.ServiceType == typeof(IVersionFlowCatalog)).Select(d => d.ImplementationInstance as IVersionFlowCatalog
            ?? throw new VersionFlowRegistrationException("Register catalogs using AddVersionFlowCatalog before AddVersionFlowDispatchers.")).ToArray();
        var catalog = new VersionFlowCatalog(catalogs, []);
        var markers = services.Where(d => d.ImplementationInstance is DispatcherRegistration).Select(d => (DispatcherRegistration)d.ImplementationInstance!).ToArray();
        foreach (var descriptor in catalog.Contracts.Values)
        {
            var existing = services.Where(d => d.ServiceType == descriptor.ContractType && !d.IsKeyedService).ToArray();
            var marker = markers.SingleOrDefault(m => m.Descriptor.ServiceType == descriptor.ContractType);
            if (existing.Length > 0 && !(existing.Length == 1 && marker?.Descriptor == existing[0]))
                throw new VersionFlowRegistrationException($"Contract '{descriptor.ContractType}' already has an unkeyed DI registration; VersionFlow will not replace it.");
        }
        foreach (var descriptor in catalog.Contracts.Values)
        {
            if (markers.Any(m => m.Descriptor.ServiceType == descriptor.ContractType)) continue;
            var registration = ServiceDescriptor.Scoped(descriptor.ContractType, p => descriptor.CreateFacade(p, null));
            services.Add(registration);
            services.AddSingleton(new DispatcherRegistration(registration));
        }
        // Capture descriptors by reference, so registrations appended after this call are also checked at startup.
        services.TryAddSingleton(new RegistrationAudit(services));
        return services;
    }
    internal sealed record DispatcherRegistration(ServiceDescriptor Descriptor);
    internal sealed class RegistrationAudit(IServiceCollection services)
    {
        public void Validate()
        {
            foreach (var marker in services.Where(d => d.ImplementationInstance is DispatcherRegistration).Select(d => (DispatcherRegistration)d.ImplementationInstance!))
                if (services.Count(d => d.ServiceType == marker.Descriptor.ServiceType && !d.IsKeyedService) != 1 || !services.Contains(marker.Descriptor))
                    throw new VersionFlowRegistrationException($"Conflicting registration for '{marker.Descriptor.ServiceType}' added after VersionFlow dispatchers.");
        }
    }
    private sealed class VersionFlowStartupValidation(IOptionsFactory<VersionFlowOptions> factory, IServiceProvider provider, VersionFlowCatalog catalog, IEnumerable<IConfigureOptions<VersionFlowOptions>> configurations, IEnumerable<IPostConfigureOptions<VersionFlowOptions>> postConfigurations) : IHostedService
    {
        public Task StartAsync(CancellationToken cancellationToken)
        {
            var startupOptions = new VersionFlowOptions();
            foreach (var configure in configurations)
                if (configure is IConfigureNamedOptions<VersionFlowOptions> named) named.Configure(Options.DefaultName, startupOptions);
                else configure.Configure(startupOptions);
            foreach (var configure in postConfigurations) configure.PostConfigure(Options.DefaultName, startupOptions);
            if (!startupOptions.ValidateOnStart) return Task.CompletedTask;
            VersionFlowOptions options;
            try { options = factory.Create(Options.DefaultName); }
            catch (OptionsValidationException e) { throw new VersionFlowConfigurationException(e.Message); }
            if (options.ValidateOnStart)
            {
                _ = provider.GetRequiredService<SnapshotStore>();
                provider.GetService<RegistrationAudit>()?.Validate();
                if (provider.GetService<IServiceProviderIsService>() is { } registrations)
                    foreach (var type in catalog.Contracts.Values.SelectMany(c => c.ImplementationTypes).Distinct())
                        if (!registrations.IsService(type)) throw new VersionFlowRegistrationException($"Implementation '{type}' must be registered by concrete type in DI.");
            }
            return Task.CompletedTask;
        }
        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}




