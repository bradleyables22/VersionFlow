using System.Reflection;
using System.Reflection.Emit;
using Microsoft.Extensions.DependencyInjection;
using VersionFlow.Extensions.Cqrs;
using VersionFlow.Extensions.DI;
using Xunit;

namespace VersionFlow.Tests;

public sealed class ExtensionTests
{
    [Fact]
    public void General_scanner_registers_arbitrary_services_with_scoped_identity_per_binding()
    {
        var services = new ServiceCollection();
        services.AddAttributedServicesFromAssemblyContaining<ExtensionTests>();
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        using var scope = provider.CreateScope(); using var other = provider.CreateScope();
        var concrete = scope.ServiceProvider.GetRequiredService<ScopedUtility>();
        var contract = scope.ServiceProvider.GetRequiredService<IUtility>();
        Assert.NotSame(concrete, contract);
        Assert.Same(contract, scope.ServiceProvider.GetRequiredService<IUtility>());
        Assert.NotSame(concrete, scope.ServiceProvider.GetRequiredService<UtilityBase>());
        Assert.Same(concrete, scope.ServiceProvider.GetRequiredService<ScopedUtility>());
        Assert.NotSame(concrete, other.ServiceProvider.GetRequiredService<ScopedUtility>());
        Assert.Same(provider.GetRequiredService<SharedDependency>(), concrete.Dependency);
        Assert.Null(scope.ServiceProvider.GetService<UnmarkedUtility>());
        Assert.Null(scope.ServiceProvider.GetService<UnmarkedDerivedUtility>());
    }

    [Fact]
    public void Native_singleton_and_transient_lifetimes_are_supported()
    {
        var services = new ServiceCollection(); services.AddAttributedServices(typeof(ExtensionTests).Assembly);
        using var provider = services.BuildServiceProvider(); using var a = provider.CreateScope(); using var b = provider.CreateScope();
        Assert.Same(a.ServiceProvider.GetRequiredService<SharedDependency>(), b.ServiceProvider.GetRequiredService<SharedDependency>());
        Assert.NotSame(a.ServiceProvider.GetRequiredService<SharedDependency>(), b.ServiceProvider.GetRequiredService<ISharedDependency>());
        Assert.Same(a.ServiceProvider.GetRequiredService<ISharedDependency>(), b.ServiceProvider.GetRequiredService<ISharedDependency>());
        Assert.NotSame(a.ServiceProvider.GetRequiredService<TransientUtility>(), a.ServiceProvider.GetRequiredService<TransientUtility>());
        Assert.NotSame(a.ServiceProvider.GetRequiredService<ITransientUtility>(), a.ServiceProvider.GetRequiredService<ITransientUtility>());
    }

    [Fact]
    public void Parameterless_scanners_use_calling_assembly_and_are_idempotent()
    {
        var services = new ServiceCollection(); services.AddAttributedServices();
        var count = services.Count;
        services.AddAttributedServices(typeof(ExtensionTests).Assembly, typeof(ExtensionTests).Assembly);
        services.AddAttributedServices();
        Assert.Equal(count, services.Count);
        Assert.Contains(services, d => d.ServiceType == typeof(ScopedUtility));
        Assert.Contains(services, d => d.ServiceType == typeof(ResultHandler));
    }

    [Fact]
    public async Task Cqrs_interfaces_bind_through_general_di_and_calls_are_direct_and_typed()
    {
        var services = new ServiceCollection(); services.AddSingleton<SharedDependency>();
        services.AddAttributedServicesFromAssemblyContaining<ExtensionTests>();
        Assert.Contains(services, d => d.ServiceType == typeof(ScopedUtility));
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true }); using var scope = provider.CreateScope();
        var concrete = scope.ServiceProvider.GetRequiredService<ResultHandler>();
        var handler = scope.ServiceProvider.GetRequiredService<ICqrsHandler<Request, string>>();
        Assert.NotSame(concrete, handler);
        Assert.Same(handler, scope.ServiceProvider.GetRequiredService<ICqrsHandler<Request, string>>());
        using var cts = new CancellationTokenSource();
        Assert.Equal("value:42", await handler.HandleAsync(new Request(42), cts.Token));
        Assert.Equal(cts.Token, ((ResultHandler)handler).LastToken);
        var noResult = scope.ServiceProvider.GetRequiredService<ICqrsHandler<Request>>();
        await noResult.HandleAsync(new Request(7), cts.Token);
        Assert.Equal(7, provider.GetRequiredService<SharedDependency>().LastValue);
        cts.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => handler.HandleAsync(new Request(0), cts.Token));
    }

    [Fact]
    public void Cqrs_supports_all_native_lifetimes_and_multiple_contracts()
    {
        var services = new ServiceCollection(); services.AddSingleton<SharedDependency>(); services.AddAttributedServices();
        using var provider = services.BuildServiceProvider(); using var a = provider.CreateScope(); using var b = provider.CreateScope();
        Assert.NotSame(a.ServiceProvider.GetRequiredService<ResultHandler>(), b.ServiceProvider.GetRequiredService<ResultHandler>());
        Assert.Same(a.ServiceProvider.GetRequiredService<NoResultHandler>(), b.ServiceProvider.GetRequiredService<NoResultHandler>());
        Assert.NotSame(a.ServiceProvider.GetRequiredService<TransientHandler>(), a.ServiceProvider.GetRequiredService<TransientHandler>());
        Assert.NotSame(a.ServiceProvider.GetRequiredService<ResultHandler>(), a.ServiceProvider.GetRequiredService<ICqrsHandler<string, string>>());
    }

    [Fact]
    public void Registration_never_constructs_services()
    {
        var services = new ServiceCollection(); services.AddAttributedServices();
        Assert.Contains(services, d => d.ServiceType == typeof(ThrowsOnConstruction));
        using var provider = services.BuildServiceProvider();
        Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService<ThrowsOnConstruction>());
    }

    [Fact]
    public void Existing_identical_concrete_registration_is_reused()
    {
        var services = new ServiceCollection(); services.AddScoped<ScopedUtility>(); services.AddAttributedServices();
        Assert.Single(services, d => d.ServiceType == typeof(ScopedUtility));
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public void Conflicts_fail_without_mutating_the_service_collection(bool additionalRegistration)
    {
        var services = new ServiceCollection();
        if (additionalRegistration) { services.AddScoped<ScopedUtility>(); services.AddTransient<ScopedUtility>(); }
        else services.AddSingleton<ScopedUtility>();
        var before = services.ToArray();
        Assert.Throws<InvalidOperationException>(() => services.AddAttributedServices());
        Assert.Equal(before, services.ToArray());
    }

    [Fact]
    public void Interface_conflicts_are_rejected()
    {
        var services = new ServiceCollection(); services.AddScoped<IUtility, AlternativeUtility>();
        Assert.Throws<InvalidOperationException>(() => services.AddAttributedServices());
        Assert.Single(services);
    }

    [Fact]
    public void Empty_null_inputs_and_invalid_lifetimes_are_rejected()
    {
        var services = new ServiceCollection();
        Assert.Throws<ArgumentException>(() => services.AddAttributedServices(Array.Empty<Assembly>()));
        Assert.Throws<ArgumentNullException>(() => services.AddAttributedServices((Assembly[])null!));
        Assert.Throws<ArgumentException>(() => services.AddAttributedServices([null!]));
        Assert.Throws<ArgumentNullException>(() => AttributedServiceCollectionExtensions.AddAttributedServices(null!, typeof(ExtensionTests).Assembly));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ServiceAttribute((ServiceLifetime)99));
        Assert.Throws<ArgumentNullException>(() => new ServiceAttribute((Type)null!));
        Assert.Throws<ArgumentException>(() => new ServiceAttribute(ServiceLifetime.Scoped, [null!]));
    }

    [Theory] [InlineData("abstract")] [InlineData("generic")] [InlineData("contract")] [InlineData("constructor")]
    public void Invalid_attributed_types_fail_clearly_and_atomically(string kind)
    {
        var assembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("InvalidFixture" + Guid.NewGuid().ToString("N")), AssemblyBuilderAccess.Run);
        var module = assembly.DefineDynamicModule("Main");
        var type = module.DefineType("InvalidService", TypeAttributes.Public | (kind == "abstract" ? TypeAttributes.Abstract : TypeAttributes.Class));
        type.DefineDefaultConstructor(kind == "constructor" ? MethodAttributes.Private : MethodAttributes.Public);
        if (kind == "generic") type.DefineGenericParameters("T");
        type.SetCustomAttribute(new CustomAttributeBuilder(typeof(ServiceAttribute).GetConstructor([typeof(ServiceLifetime), typeof(Type[])])!,
            [ServiceLifetime.Scoped, kind == "contract" ? new[] { typeof(IDisposable) } : Array.Empty<Type>()]));
        type.CreateType();
        var services = new ServiceCollection();
        var error = Assert.Throws<InvalidOperationException>(() => services.AddAttributedServices(assembly));
        Assert.Contains("InvalidService", error.Message); Assert.Empty(services);
    }

    [Fact]
    public void Attributed_implementations_work_with_versionflow_without_manual_concrete_registration()
    {
        var services = new ServiceCollection(); services.AddAttributedServices();
        services.AddVersionFlowCatalog<Generated.Assembly_VersionFlow_Tests.GeneratedCatalog>();
        services.AddVersionFlowOptions(); services.AddVersionFlowDispatchers();
        using var provider = services.BuildServiceProvider(); using var scope = provider.CreateScope();
        Assert.Equal(2, scope.ServiceProvider.GetRequiredService<IVersionedUtility>().Get());
        Assert.Equal(1, scope.ServiceProvider.GetRequiredService<IVersionFlow>().For<IVersionedUtility>().UseVersion(1).Resolve().Get());
    }
}

public interface IUtility;
public abstract class UtilityBase;
public interface ISharedDependency;
public interface ITransientUtility;
[Service(ServiceLifetime.Singleton, typeof(ISharedDependency))]
public sealed class SharedDependency : ISharedDependency { public int LastValue; }
[Service(ServiceLifetime.Scoped, typeof(IUtility), typeof(UtilityBase))]
public class ScopedUtility(SharedDependency dependency) : UtilityBase, IUtility { public SharedDependency Dependency { get; } = dependency; }
public sealed class UnmarkedDerivedUtility(SharedDependency dependency) : ScopedUtility(dependency);
[Service(ServiceLifetime.Transient, typeof(ITransientUtility))]
public sealed class TransientUtility : ITransientUtility;
public sealed class UnmarkedUtility;
[Service(ServiceLifetime.Transient)]
public sealed class ThrowsOnConstruction { public ThrowsOnConstruction() => throw new InvalidOperationException("Expected constructor exception"); }
public sealed record Request(int Value);
[Service(ServiceLifetime.Scoped, typeof(ICqrsHandler<Request, string>), typeof(ICqrsHandler<string, string>))]
public sealed class ResultHandler(SharedDependency dependency) : ICqrsHandler<Request, string>, ICqrsHandler<string, string>
{
    public CancellationToken LastToken { get; private set; }
    public Task<string> HandleAsync(Request request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested(); LastToken = cancellationToken; dependency.LastValue = request.Value;
        return Task.FromResult("value:" + request.Value);
    }
    public Task<string> HandleAsync(string request, CancellationToken cancellationToken = default) => Task.FromResult(request);
}
[Service(typeof(ICqrsHandler<Request>), ServiceLifetime.Singleton)]
public sealed class NoResultHandler(SharedDependency dependency) : ICqrsHandler<Request>
{
    public Task HandleAsync(Request request, CancellationToken cancellationToken = default)
    { cancellationToken.ThrowIfCancellationRequested(); dependency.LastValue = request.Value; return Task.CompletedTask; }
}
[Service(ServiceLifetime.Transient, AsImplementedInterfaces = true)]
public sealed class TransientHandler : ICqrsHandler<int> { public Task HandleAsync(int request, CancellationToken cancellationToken = default) => Task.CompletedTask; }
public interface IVersionedUtility { int Get(); }
[Service] [Versioned(typeof(IVersionedUtility), 1, Key = "Utility")]
public sealed class VersionedUtility1 : IVersionedUtility { public int Get() => 1; }
[Service] [Versioned(typeof(IVersionedUtility), 2, Key = "Utility")]
public sealed class VersionedUtility2 : IVersionedUtility { public int Get() => 2; }


public sealed class AlternativeUtility : IUtility;


