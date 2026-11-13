using System.Reflection;
using System.Reflection.Emit;
using Microsoft.Extensions.DependencyInjection;
using VersionFlow.Extensions.DI;
using Xunit;

namespace VersionFlow.Tests;

public sealed class ServiceDisposalTests
{
    public static TheoryData<ServiceLifetime, Type, bool> DisposalCases
    {
        get
        {
            var cases = new TheoryData<ServiceLifetime, Type, bool>();
            foreach (var lifetime in Enum.GetValues<ServiceLifetime>())
                foreach (var probe in new[] { typeof(SyncDisposalProbe), typeof(AsyncDisposalProbe), typeof(DualDisposalProbe) })
                    foreach (var automaticInterfaces in new[] { false, true })
                        cases.Add(lifetime, probe, automaticInterfaces);
            return cases;
        }
    }

    [Theory]
    [MemberData(nameof(DisposalCases))]
    public async Task Every_created_instance_is_disposed_exactly_once(ServiceLifetime lifetime, Type probe, bool automaticInterfaces)
    {
        var (assembly, implementation) = CreateFixture(lifetime, probe, automaticInterfaces);
        var services = new ServiceCollection();
        services.AddAttributedServices(assembly);
        services.AddAttributedServices(assembly);
        Assert.Equal(3, services.Count);
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        var first = provider.CreateAsyncScope();
        var second = provider.CreateAsyncScope();
        var firstInstances = ResolveBindings(first.ServiceProvider, implementation, lifetime);
        var secondInstances = ResolveBindings(second.ServiceProvider, implementation, lifetime);
        foreach (var instance in firstInstances.Concat(secondInstances)) Assert.Equal(0, instance.TotalDisposals);

        if (lifetime == ServiceLifetime.Singleton)
            for (var i = 0; i < firstInstances.Length; i++) Assert.Same(firstInstances[i], secondInstances[i]);
        else
            foreach (var instance in firstInstances) Assert.DoesNotContain(secondInstances, other => ReferenceEquals(instance, other));

        await first.DisposeAsync();
        foreach (var instance in firstInstances) Assert.Equal(lifetime == ServiceLifetime.Singleton ? 0 : 1, instance.TotalDisposals);
        foreach (var instance in secondInstances) Assert.Equal(0, instance.TotalDisposals);
        await second.DisposeAsync();
        await provider.DisposeAsync();
        // Repeated provider/scope disposal must not cause another instance disposal.
        await first.DisposeAsync(); await second.DisposeAsync(); await provider.DisposeAsync();
        foreach (var instance in firstInstances.Concat(secondInstances))
        {
            Assert.Equal(1, instance.TotalDisposals);
            Assert.Equal(probe == typeof(SyncDisposalProbe) ? 1 : 0, instance.SyncDisposals);
            Assert.Equal(probe == typeof(SyncDisposalProbe) ? 0 : 1, instance.AsyncDisposals);
        }
    }

    [Theory]
    [InlineData(ServiceLifetime.Scoped)]
    [InlineData(ServiceLifetime.Singleton)]
    [InlineData(ServiceLifetime.Transient)]
    public void Synchronous_disposal_uses_Dispose_once_per_binding(ServiceLifetime lifetime)
    {
        var (assembly, implementation) = CreateFixture(lifetime, typeof(DualDisposalProbe), false);
        var services = new ServiceCollection(); services.AddAttributedServices(assembly);
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        var scope = provider.CreateScope();
        var instances = ResolveBindings(scope.ServiceProvider, implementation, lifetime);
        scope.Dispose(); provider.Dispose(); scope.Dispose(); provider.Dispose();
        foreach (var instance in instances) { Assert.Equal(1, instance.SyncDisposals); Assert.Equal(0, instance.AsyncDisposals); }
    }

    [Fact]
    public async Task Interface_only_resolution_does_not_construct_the_concrete_binding()
    {
        ConstructionCountingProbe.Constructions = 0;
        var (assembly, _) = CreateFixture(ServiceLifetime.Scoped, typeof(ConstructionCountingProbe), true);
        var services = new ServiceCollection(); services.AddAttributedServices(assembly);
        await using var provider = services.BuildServiceProvider();
        var scope = provider.CreateAsyncScope();
        Assert.Equal(0, ConstructionCountingProbe.Constructions);
        var instance = (DisposalProbe)scope.ServiceProvider.GetRequiredService<IFirstDisposalContract>();
        Assert.Equal(1, ConstructionCountingProbe.Constructions);
        await scope.DisposeAsync();
        Assert.Equal(1, instance.AsyncDisposals);
    }

    [Fact]
    public async Task Existing_native_interface_registration_is_reused_and_disposed_once()
    {
        var (assembly, implementation) = CreateFixture(ServiceLifetime.Scoped, typeof(AsyncDisposalProbe), false);
        var services = new ServiceCollection();
        services.AddScoped(typeof(IFirstDisposalContract), implementation);
        services.AddAttributedServices(assembly);
        Assert.Single(services, d => d.ServiceType == typeof(IFirstDisposalContract));
        await using var provider = services.BuildServiceProvider();
        var scope = provider.CreateAsyncScope();
        var instances = ResolveBindings(scope.ServiceProvider, implementation, ServiceLifetime.Scoped);
        await scope.DisposeAsync();
        foreach (var instance in instances) Assert.Equal(1, instance.AsyncDisposals);
    }

    private static DisposalProbe[] ResolveBindings(IServiceProvider provider, Type implementation, ServiceLifetime lifetime)
    {
        // Mix interface, base concrete, and IEnumerable resolution, in both orders across calls.
        var contracts = new[] { typeof(IFirstDisposalContract), implementation, typeof(ISecondDisposalContract) };
        var first = contracts.Select(t => (DisposalProbe)provider.GetRequiredService(t)).ToArray();
        var second = contracts.Select(t => (DisposalProbe)provider.GetRequiredService(t)).ToArray();
        Assert.Equal(3, first.Distinct(ReferenceEqualityComparer.Instance).Count());
        for (var i = 0; i < first.Length; i++)
            if (lifetime == ServiceLifetime.Transient) Assert.NotSame(first[i], second[i]);
            else Assert.Same(first[i], second[i]);
        var enumerated = (DisposalProbe)Assert.Single(provider.GetServices<IFirstDisposalContract>());
        if (lifetime != ServiceLifetime.Transient) Assert.Same(first[0], enumerated);
        return first.Concat(second).Append(enumerated).Distinct().ToArray();
    }

    private static (Assembly Assembly, Type Implementation) CreateFixture(ServiceLifetime lifetime, Type parent, bool automaticInterfaces)
    {
        var assembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("DisposalFixture" + Guid.NewGuid().ToString("N")), AssemblyBuilderAccess.Run);
        var builder = assembly.DefineDynamicModule("Main").DefineType("DisposableService", TypeAttributes.Public | TypeAttributes.Sealed, parent);
        builder.DefineDefaultConstructor(MethodAttributes.Public);
        builder.SetCustomAttribute(new CustomAttributeBuilder(
            typeof(ServiceAttribute).GetConstructor([typeof(ServiceLifetime), typeof(Type[])])!,
            [lifetime, automaticInterfaces ? Array.Empty<Type>() : new[] { typeof(IFirstDisposalContract), typeof(ISecondDisposalContract) }],
            [typeof(ServiceAttribute).GetProperty(nameof(ServiceAttribute.AsImplementedInterfaces))!], [automaticInterfaces]));
        return (assembly, builder.CreateType()!);
    }
}

public interface IFirstDisposalContract;
public interface ISecondDisposalContract;
public abstract class DisposalProbe : IFirstDisposalContract, ISecondDisposalContract
{
    public int SyncDisposals { get; protected set; }
    public int AsyncDisposals { get; protected set; }
    public int TotalDisposals => SyncDisposals + AsyncDisposals;
    protected void AssertFirstDisposal()
    {
        if (TotalDisposals != 1) throw new InvalidOperationException("Instance disposed more than once.");
    }
}
public class SyncDisposalProbe : DisposalProbe, IDisposable
{
    public void Dispose() { SyncDisposals++; AssertFirstDisposal(); }
}
public class AsyncDisposalProbe : DisposalProbe, IAsyncDisposable
{
    public async ValueTask DisposeAsync() { await Task.Yield(); AsyncDisposals++; AssertFirstDisposal(); }
}
public class DualDisposalProbe : DisposalProbe, IDisposable, IAsyncDisposable
{
    public void Dispose() { SyncDisposals++; AssertFirstDisposal(); }
    public async ValueTask DisposeAsync() { await Task.Yield(); AsyncDisposals++; AssertFirstDisposal(); }
}

public class ConstructionCountingProbe : AsyncDisposalProbe
{
    public static int Constructions;
    public ConstructionCountingProbe() => Interlocked.Increment(ref Constructions);
}
