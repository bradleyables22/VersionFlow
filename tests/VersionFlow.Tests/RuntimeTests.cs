using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using VersionFlow.Generated.Assembly_VersionFlow_Tests;
using Xunit;
namespace VersionFlow.Tests;

public sealed class RuntimeTests
{
    internal static ServiceCollection Services(ServiceLifetime lifetime = ServiceLifetime.Scoped)
    {
        var services = new ServiceCollection();
        services.AddSingleton<FirstDependency>(); services.AddSingleton<SecondDependency>();
        ((IServiceCollection)services).Add(new ServiceDescriptor(typeof(Price1), typeof(Price1), lifetime));
        ((IServiceCollection)services).Add(new ServiceDescriptor(typeof(Price2), typeof(Price2), lifetime));
        services.AddScoped<Operations>();
        services.AddScoped<OldOrder>(); services.AddScoped<NewOrder>();
        services.AddVersionFlowCatalog<GeneratedCatalog>();
        services.AddVersionFlowOperation("Orders.Process");
        return services;
    }
    private static ServiceProvider Build(IServiceCollection services) => services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });

    [Fact] public async Task Typed_facades_use_method_class_global_and_explicit_precedence()
    {
        var services = Services();
        services.AddVersionFlow(o => { o.Overrides.Classes["Pricing"] = new() { Version = "1", Methods = { ["Calculate"] = "2" } }; });
        services.AddVersionFlowDispatchers();
        using var provider = Build(services); using var scope = provider.CreateScope();
        var price = scope.ServiceProvider.GetRequiredService<IPrice>();
        Assert.Equal(18m, price.Calculate(2)); Assert.Equal(95m, price.Discount(100));
        price.Touch(); Assert.Equal("hello", await price.EchoAsync("hello", default));
        var flow = scope.ServiceProvider.GetRequiredService<IVersionFlow>();
        Assert.Equal(VersionSelectionSource.Method, flow.Select("Pricing", "Calculate").Source);
        Assert.Equal(VersionSelectionSource.Class, flow.Select("Pricing", "Discount").Source);
        var original = flow.For<IPrice>();
        Assert.Equal(20m, original.UseVersion(1).Resolve().Calculate(2));
        Assert.Equal(18m, original.Resolve().Calculate(2));
        Assert.Equal(90m, original.UseLatest().Resolve().Discount(100));
        Assert.Equal(VersionSelectionSource.Explicit, original.UseLatest().Selection.Source);
        Assert.Equal(1, original.Selection.Number);
    }
    [Fact] public void Independent_methods_have_independent_latest_versions()
    {
        var services = Services(); services.AddVersionFlow(o => o.Overrides.Classes["Ops"] = new() { Methods = { ["Calculate"] = "1" } }); services.AddVersionFlowDispatchers();
        using var provider = Build(services); using var scope = provider.CreateScope();
        var flow = scope.ServiceProvider.GetRequiredService<IVersionFlow>();
        var ops = flow.For<IOperations>().Resolve();
        Assert.Equal(5, ops.Calculate(5)); Assert.Equal(4, ops.Discount(5));
        Assert.Equal(15, flow.For<IOperations>().UseLatest().Resolve().Calculate(5));
        Assert.Equal(1, flow.Select("Ops", "Discount").Number);
        Assert.Throws<VersionFlowSelectionException>(() => flow.For<IOperations>().Selection);
        Assert.Throws<VersionFlowSelectionException>(() => flow.For<IOperations>().UseVersion(3).Resolve().Discount(5));
    }
    [Theory] [InlineData(ServiceLifetime.Scoped)] [InlineData(ServiceLifetime.Singleton)] [InlineData(ServiceLifetime.Transient)]
    public void Concrete_lifetimes_are_preserved(ServiceLifetime lifetime)
    {
        var services = Services(lifetime); services.AddVersionFlowOptions(); services.AddVersionFlowDispatchers();
        using var provider = Build(services); using var scope = provider.CreateScope(); using var other = provider.CreateScope();
        var first = scope.ServiceProvider.GetRequiredService<IPrice>();
        var a = first.Identity(); var b = first.Identity(); var c = other.ServiceProvider.GetRequiredService<IPrice>().Identity();
        Assert.Equal(lifetime != ServiceLifetime.Transient, a == b);
        Assert.Equal(lifetime == ServiceLifetime.Singleton, a == c);
        Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService<IPrice>());
    }
    [Fact] public void Resolution_is_lazy_and_missing_registration_is_explicit()
    {
        var services = Services();
        foreach (var descriptor in services.Where(d => d.ServiceType == typeof(Price2)).ToArray()) services.Remove(descriptor);
        services.AddVersionFlow(o => o.Overrides.Classes["Pricing"] = new() { Version = "1" }); services.AddVersionFlowDispatchers();
        using var provider = Build(services); using var scope = provider.CreateScope();
        var flow = scope.ServiceProvider.GetRequiredService<IVersionFlow>();
        Assert.Equal(10m, flow.For<IPrice>().Resolve().Calculate(1));
        var error = Assert.Throws<VersionFlowRegistrationException>(() => flow.For<IPrice>().UseLatest().Resolve().Calculate(1));
        Assert.Contains("concrete type", error.Message);
        Assert.Equal(0, provider.GetRequiredService<SecondDependency>().Calls);
    }
    [Fact] public void Missing_versions_are_descriptive_and_never_fall_back()
    {
        var services = Services(); services.AddVersionFlowOptions();
        using var provider = Build(services); using var scope = provider.CreateScope();
        var flow = scope.ServiceProvider.GetRequiredService<IVersionFlow>();
        var e = Assert.Throws<VersionFlowSelectionException>(() => flow.For<IPrice>().UseVersion(9).Resolve().Calculate(1));
        Assert.Contains("9", e.Message); Assert.Contains("Pricing", e.Message); Assert.Contains("Calculate", e.Message); Assert.Contains("1, 2", e.Message);
        Assert.Throws<ArgumentOutOfRangeException>(() => flow.For<IPrice>().UseVersion(0));
    }
    [Fact] public async Task Fluent_callbacks_only_resolve_and_execute_selected_branch()
    {
        var services = Services(); services.AddVersionFlow(o => o.Overrides.Classes["Orders"] = new() { Methods = { ["Process"] = "2" } });
        using var provider = Build(services); using var scope = provider.CreateScope();
        var flow = scope.ServiceProvider.GetRequiredService<IVersionFlow>();
        var empty = flow.For("Orders.Process").Match<string>();
        var match = empty.Version<NotRegistered>(1, _ => throw new Exception("Should not run")).Version<NewOrder>(2, s => s.Process(7, true));
        Assert.Equal("new:7:True", match.Execute());
        Assert.Throws<VersionFlowSelectionException>(() => empty.Execute());
        Assert.Throws<ArgumentException>(() => match.Version<NewOrder>(2, _ => "duplicate"));
        Assert.Equal("old:7", flow.For("Orders.Process").UseVersion(1).Match<string>().Version<OldOrder>(1, s => s.Process(7)).Execute());
        CancellationToken seen = default;
        using var cts = new CancellationTokenSource();
        var asyncMatch = flow.For("Orders.Process").MatchAsync<string>().Version<NotRegistered>(1, _ => throw new Exception())
            .Version<NewOrder>(2, (s, ct) => { seen = ct; return s.ProcessAsync(7, true, ct); });
        Assert.Equal("new:7:True", await asyncMatch.ExecuteAsync(cts.Token)); Assert.Equal(cts.Token, seen);
        cts.Cancel(); await Assert.ThrowsAsync<OperationCanceledException>(() => asyncMatch.ExecuteAsync(cts.Token));
        var expected = new InvalidOperationException("callback");
        Assert.Same(expected, Assert.Throws<InvalidOperationException>(() => flow.For("Orders.Process").Match<string>().Version<NewOrder>(2, _ => throw expected).Execute()));
    }
    private sealed class NotRegistered;

    [Theory] [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void All_options_setup_styles_and_options_interfaces_work(int style)
    {
        var services = Services();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["VersionFlow:Overrides:Classes:Pricing:Version"] = "1" }).Build();
        if (style == 0) services.AddVersionFlow(configuration.GetSection("VersionFlow"));
        else if (style == 1) services.AddVersionFlow(o => o.Overrides.Classes["Pricing"] = new() { Version = "1" });
        else services.AddVersionFlowOptions().Bind(configuration.GetSection("VersionFlow")).ValidateOnStart();
        services.Configure<VersionFlowOptions>(o => o.Overrides.Classes["Pricing"].Methods["Calculate"] = "2");
        services.AddVersionFlowOptions(); services.AddVersionFlowDispatchers(); services.AddVersionFlowDispatchers();
        using var provider = Build(services); using var scope = provider.CreateScope();
        Assert.Equal("1", provider.GetRequiredService<IOptions<VersionFlowOptions>>().Value.Overrides.Classes["Pricing"].Version);
        Assert.Equal("1", provider.GetRequiredService<IOptionsMonitor<VersionFlowOptions>>().CurrentValue.Overrides.Classes["Pricing"].Version);
        Assert.Equal("2", scope.ServiceProvider.GetRequiredService<IOptionsSnapshot<VersionFlowOptions>>().Value.Overrides.Classes["Pricing"].Methods["Calculate"]);
        Assert.Equal(18m, scope.ServiceProvider.GetRequiredService<IPrice>().Calculate(2));
        Assert.Single(services, d => d.ServiceType == typeof(IVersionFlow));
        Assert.Single(services, d => d.ServiceType == typeof(IPrice));
    }
    [Theory] [InlineData("Default", "0")] [InlineData("Default", "latest")] [InlineData("Overrides:Classes:Unknown:Version", "1")] [InlineData("Overrides:Classes:Pricing:Methods:Unknown", "1")] [InlineData("Overrides:Classes:Pricing:Methods:Calculate", "9")] [InlineData("MissingVersionBehavior", "Fallback")]
    public void Invalid_configuration_is_rejected(string key, string value)
    {
        var services = Services(); var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { [key] = value }).Build();
        services.AddVersionFlow(configuration);
        using var provider = Build(services);
        Assert.Throws<VersionFlowConfigurationException>(() => provider.GetRequiredService<VersionFlowSelector>());
    }
    [Fact] public void Nested_method_only_override_inherits_default()
    {
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("""
            { "Default": "1", "Overrides": { "Classes": { "Pricing": { "Methods": { "Calculate": "2" } } } } }
            """));
        var configuration = new ConfigurationBuilder().AddJsonStream(stream).AddInMemoryCollection().Build();
        var services = Services(); services.AddVersionFlow(configuration);
        using var provider = Build(services);
        var selector = provider.GetRequiredService<VersionFlowSelector>();
        Assert.Equal(2, selector.Select("Pricing", "Calculate").Number);
        Assert.Equal(VersionSelectionSource.Method, selector.Select("Pricing", "Calculate").Source);
        Assert.Equal(1, selector.Select("Pricing", "Discount").Number);
        Assert.Equal(VersionSelectionSource.Global, selector.Select("Pricing", "Discount").Source);
    }
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    public void Invalid_nested_objects_are_rejected(int invalid)
    {
        var services = Services();
        services.AddVersionFlow(o =>
        {
            if (invalid == 0) o.Overrides = null!;
            else if (invalid == 1) o.Overrides.Classes = null!;
            else if (invalid == 2) o.Overrides.Classes["Pricing"] = null!;
            else if (invalid == 3) o.Overrides.Classes["Pricing"] = new() { Methods = null! };
            else o.Overrides.Classes["Unknown"] = new();
        });
        using var provider = Build(services);
        Assert.Throws<VersionFlowConfigurationException>(() => provider.GetRequiredService<VersionFlowSelector>());
    }
    [Fact] public void Dispatcher_registration_conflicts_are_never_replaced()
    {
        var services = Services(); services.AddScoped<IPrice, Price1>();
        Assert.Throws<VersionFlowRegistrationException>(() => services.AddVersionFlowDispatchers());
        Assert.Single(services, d => d.ServiceType == typeof(IPrice));
    }
    [Fact] public async Task Startup_detects_late_conflicts_and_missing_concrete_registrations()
    {
        foreach (var conflict in new[] { true, false })
        {
            var builder = Host.CreateApplicationBuilder();
            foreach (var descriptor in Services()) builder.Services.Add(descriptor);
            builder.Services.AddVersionFlowDispatchers();
            if (conflict) builder.Services.AddScoped<IPrice, Price1>();
            else foreach (var d in builder.Services.Where(d => d.ServiceType == typeof(Price2)).ToArray()) builder.Services.Remove(d);
            using var host = builder.Build();
            await Assert.ThrowsAsync<VersionFlowRegistrationException>(() => host.StartAsync());
        }
    }
    [Fact] public void Valid_reload_is_atomic_and_invalid_reload_retains_previous_snapshot()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();
        var services = Services(); services.AddVersionFlow(configuration); services.AddVersionFlowDispatchers();
        using var provider = Build(services); using var scope = provider.CreateScope();
        var flow = scope.ServiceProvider.GetRequiredService<IVersionFlow>(); var facade = flow.For<IPrice>().Resolve();
        Assert.Equal(9m, facade.Calculate(1)); var initial = flow.Select("Pricing", "Calculate");
        configuration["Overrides:Classes:Pricing:Version"] = "1"; configuration.Reload();
        Assert.Equal(10m, facade.Calculate(1)); var valid = flow.Select("Pricing", "Calculate"); Assert.True(valid.SnapshotIdentity > initial.SnapshotIdentity);
        configuration["Overrides:Classes:Pricing:Version"] = "777"; configuration.Reload();
        Assert.Equal(10m, facade.Calculate(1)); Assert.Equal(valid.SnapshotIdentity, flow.Select("Pricing", "Calculate").SnapshotIdentity);
        configuration["Overrides:Classes:Pricing:Version"] = "2"; configuration.Reload(); Assert.Equal(9m, facade.Calculate(1));
    }
    [Fact] public async Task Concurrent_selections_observe_complete_snapshots()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Overrides:Classes:Pricing:Version"] = "1" }).Build();
        var services = Services(); services.AddVersionFlow(configuration);
        using var provider = Build(services); var selector = provider.GetRequiredService<VersionFlowSelector>();
        var writer = Task.Run(() => { for (var i = 0; i < 100; i++) { configuration["Overrides:Classes:Pricing:Version"] = i % 2 == 0 ? "2" : "1"; configuration.Reload(); } });
        var readers = Enumerable.Range(0, 8).Select(_ => Task.Run(() =>
        {
            for (var i = 0; i < 1000; i++)
            {
                var selected = selector.Select("Pricing", "Calculate");
                Assert.Contains(selected.Number, new[] { 1, 2 }); Assert.Equal(VersionSelectionSource.Class, selected.Source);
                Assert.Equal(new[] { 1, 2 }, selected.AvailableVersions); Assert.True(selected.SnapshotIdentity > 0);
            }
        }));
        await Task.WhenAll(readers.Append(writer));
    }
}

