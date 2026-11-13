using Microsoft.Extensions.DependencyInjection;
using VersionFlow.Extensions.Cqrs;
using VersionFlow.Extensions.DI;
using Xunit;

namespace VersionFlow.Tests;

public sealed class InterfaceBindingTests
{
    [Fact]
    public async Task Cqrs_can_be_used_without_any_di_or_registry()
    {
        ICqrsHandler<int, int> handler = new StandaloneHandler();
        Assert.Equal(12, await handler.HandleAsync(6));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => handler.HandleAsync(6, cancellation.Token));
        var references = typeof(ICqrsHandler<>).Assembly.GetReferencedAssemblies();
        Assert.DoesNotContain(references, reference => reference.Name!.StartsWith("VersionFlow.", StringComparison.Ordinal)
            || reference.Name.StartsWith("Microsoft.Extensions.", StringComparison.Ordinal));
    }

    [Fact]
    public void Cqrs_can_use_native_di_without_the_attribute_extension()
    {
        var services = new ServiceCollection();
        services.AddScoped<ICqrsHandler<int, int>, StandaloneHandler>();
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using var scope = provider.CreateScope();
        Assert.IsType<StandaloneHandler>(scope.ServiceProvider.GetRequiredService<ICqrsHandler<int, int>>());
    }

    [Fact]
    public void Interface_discovery_includes_inherited_and_closed_generic_interfaces_and_deduplicates_explicit_bindings()
    {
        var services = new ServiceCollection(); services.AddAttributedServices(); services.AddAttributedServices();
        Assert.Single(services, d => d.ServiceType == typeof(IChildBinding));
        Assert.Single(services, d => d.ServiceType == typeof(IParentBinding));
        Assert.Single(services, d => d.ServiceType == typeof(IGenericBinding<string>));
        using var provider = services.BuildServiceProvider(); using var scope = provider.CreateScope();
        var concrete = scope.ServiceProvider.GetRequiredService<AutomaticBindings>();
        Assert.NotSame(concrete, scope.ServiceProvider.GetRequiredService<IParentBinding>());
        Assert.NotSame(concrete, scope.ServiceProvider.GetRequiredService<IChildBinding>());
        Assert.NotSame(concrete, scope.ServiceProvider.GetRequiredService<IGenericBinding<string>>());
        Assert.Single(scope.ServiceProvider.GetServices<IChildBinding>());
        Assert.Equal("bound", scope.ServiceProvider.GetRequiredService<IGenericBinding<string>>().Read());
    }

    [Fact]
    public void Automatic_interface_binding_excludes_disposal_interfaces_and_is_opt_in()
    {
        var services = new ServiceCollection(); services.AddAttributedServices();
        Assert.DoesNotContain(services, d => d.ServiceType == typeof(IDisposable) || d.ServiceType == typeof(IAsyncDisposable));
        Assert.Contains(services, d => d.ServiceType == typeof(ConcreteOnlyBinding));
        Assert.DoesNotContain(services, d => d.ServiceType == typeof(IConcreteOnlyBinding));
    }

    [Fact]
    public void Interface_first_constructor_defaults_to_scoped_and_supports_multiple_contracts()
    {
        var services = new ServiceCollection(); services.AddAttributedServices();
        Assert.Single(services, d => d.ServiceType == typeof(IExplicitBinding) && d.Lifetime == ServiceLifetime.Scoped);
        using var provider = services.BuildServiceProvider(); using var scope = provider.CreateScope();
        Assert.NotSame(scope.ServiceProvider.GetRequiredService<ExplicitBinding>(), scope.ServiceProvider.GetRequiredService<IExplicitBinding>());
        Assert.Same(scope.ServiceProvider.GetRequiredService<IExplicitBinding>(), scope.ServiceProvider.GetRequiredService<IExplicitBinding>());
        var attribute = new ServiceAttribute(typeof(IChildBinding), ServiceLifetime.Transient, typeof(IParentBinding));
        Assert.Equal(ServiceLifetime.Transient, attribute.Lifetime);
        Assert.Equal(new[] { typeof(IChildBinding), typeof(IParentBinding) }, attribute.ServiceTypes);
    }

    [Fact]
    public void Automatic_bindings_still_reject_existing_interface_conflicts_atomically()
    {
        var services = new ServiceCollection(); services.AddSingleton<IParentBinding, AutomaticBindings>();
        var original = services.ToArray();
        Assert.Throws<InvalidOperationException>(() => services.AddAttributedServices());
        Assert.Equal(original, services.ToArray());
    }

    public sealed class StandaloneHandler : ICqrsHandler<int, int>
    {
        public Task<int> HandleAsync(int request, CancellationToken cancellationToken = default)
        { cancellationToken.ThrowIfCancellationRequested(); return Task.FromResult(request * 2); }
    }
}

public interface IParentBinding;
public interface IChildBinding : IParentBinding;
public interface IGenericBinding<T> { T Read(); }
[Service(ServiceLifetime.Scoped, typeof(IChildBinding), AsImplementedInterfaces = true)]
public sealed class AutomaticBindings : IChildBinding, IGenericBinding<string>, IDisposable, IAsyncDisposable
{
    public string Read() => "bound";
    public void Dispose() { }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
public interface IConcreteOnlyBinding;
[Service]
public sealed class ConcreteOnlyBinding : IConcreteOnlyBinding;
public interface IExplicitBinding;
[Service(typeof(IExplicitBinding))]
public sealed class ExplicitBinding : IExplicitBinding;

