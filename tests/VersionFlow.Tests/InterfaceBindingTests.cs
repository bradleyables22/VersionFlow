using Microsoft.Extensions.DependencyInjection;
using VersionFlow.Extensions.Cqrs;
using VersionFlow.Extensions.DI;
using Xunit;

namespace VersionFlow.Tests;

public sealed class InterfaceBindingTests
{
    [Fact]
    public void Synchronous_cqrs_can_be_called_directly_with_or_without_a_response()
    {
        ICqrsHandler<int, int> query = new SynchronousQuery();
        var command = new SynchronousCommand();
        ICqrsHandler<int> handler = command;

        Assert.Equal(12, query.Handle(6));
        handler.Handle(7);
        Assert.Equal(7, command.LastRequest);

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => query.Handle(6, cancellation.Token));
        Assert.Throws<OperationCanceledException>(() => handler.Handle(8, cancellation.Token));
        Assert.Equal(7, command.LastRequest);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Synchronous_cqrs_supports_native_and_attribute_di(bool useAttributes)
    {
        var services = new ServiceCollection();
        if (useAttributes)
            services.AddAttributedServicesFromAssemblyContaining<InterfaceBindingTests>();
        else
        {
            services.AddScoped<ICqrsHandler<int, int>, SynchronousQuery>();
            services.AddScoped<ICqrsHandler<int>, SynchronousCommand>();
        }

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using var firstScope = provider.CreateScope();
        using var secondScope = provider.CreateScope();
        var query = firstScope.ServiceProvider.GetRequiredService<ICqrsHandler<int, int>>();
        var command = firstScope.ServiceProvider.GetRequiredService<ICqrsHandler<int>>();
        Assert.Equal(12, query.Handle(6));
        command.Handle(7);
        Assert.Equal(7, Assert.IsType<SynchronousCommand>(command).LastRequest);
        Assert.Same(query, firstScope.ServiceProvider.GetRequiredService<ICqrsHandler<int, int>>());
        Assert.NotSame(query, secondScope.ServiceProvider.GetRequiredService<ICqrsHandler<int, int>>());
    }

    [Fact]
    public async Task Cqrs_can_be_used_without_any_di_or_registry()
    {
        ICqrsAsyncHandler<int, int> handler = new StandaloneHandler();
        Assert.Equal(12, await handler.HandleAsync(6));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => handler.HandleAsync(6, cancellation.Token));
        var references = typeof(ICqrsAsyncHandler<>).Assembly.GetReferencedAssemblies();
        Assert.DoesNotContain(references, reference => reference.Name!.StartsWith("VersionFlow.", StringComparison.Ordinal)
            || reference.Name.StartsWith("Microsoft.Extensions.", StringComparison.Ordinal));
    }

    [Fact]
    public void Cqrs_can_use_native_di_without_the_attribute_extension()
    {
        var services = new ServiceCollection();
        services.AddScoped<ICqrsAsyncHandler<int, int>, StandaloneHandler>();
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using var scope = provider.CreateScope();
        Assert.IsType<StandaloneHandler>(scope.ServiceProvider.GetRequiredService<ICqrsAsyncHandler<int, int>>());
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

    public sealed class StandaloneHandler : ICqrsAsyncHandler<int, int>
    {
        public Task<int> HandleAsync(int request, CancellationToken cancellationToken = default)
        { cancellationToken.ThrowIfCancellationRequested(); return Task.FromResult(request * 2); }
    }

    [Service(typeof(ICqrsHandler<int, int>))]
    public sealed class SynchronousQuery : ICqrsHandler<int, int>
    {
        public int Handle(int request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return request * 2;
        }
    }

    [Service(typeof(ICqrsHandler<int>))]
    public sealed class SynchronousCommand : ICqrsHandler<int>
    {
        public int LastRequest { get; private set; }

        public void Handle(int request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastRequest = request;
        }
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

