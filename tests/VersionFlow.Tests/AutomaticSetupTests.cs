using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using VersionFlow.TestServices;
using Xunit;

namespace VersionFlow.Tests;

public sealed class AutomaticSetupTests
{
    private static ServiceCollection Services()
    {
        var services = RuntimeTests.Services();
        foreach (var registration in services.Where(d => d.ServiceType == typeof(IVersionFlowCatalog)).ToArray()) services.Remove(registration);
        services.AddScoped<External1>(); services.AddScoped<External5>();
        services.AddScoped<VersionedUtility1>(); services.AddScoped<VersionedUtility2>();
        return services;
    }

    [Theory] [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public void All_setup_styles_link_local_and_referenced_catalogs_and_dispatchers(int style)
    {
        var services = Services();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Overrides:Classes:Pricing:Version"] = "1", ["Overrides:Classes:Pricing:Methods:Calculate"] = "2" }).Build();
        if (style == 0) services.AddVersionFlow(configuration);
        else if (style == 1) services.AddVersionFlow(o => { o.Overrides.Classes["Pricing"] = new() { Version = "1", Methods = { ["Calculate"] = "2" } }; });
        else if (style == 2) services.AddVersionFlowOptions().Bind(configuration);
        else services.AddVersionFlow();
        services.AddVersionFlow();
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        using var scope = provider.CreateScope();
        Assert.Equal(18m, scope.ServiceProvider.GetRequiredService<IPrice>().Calculate(2));
        Assert.Equal(style == 3 ? 90m : 95m, scope.ServiceProvider.GetRequiredService<IPrice>().Discount(100));
        Assert.Equal(9, scope.ServiceProvider.GetRequiredService<IOperations>().Calculate(3));
        Assert.Equal("external-five", scope.ServiceProvider.GetRequiredService<IExternal>().Execute());
        Assert.Equal(20m, scope.ServiceProvider.GetRequiredService<IVersionFlow>().For<IPrice>().UseVersion(1).Resolve().Calculate(2));
        Assert.Single(services, d => d.ServiceType == typeof(IPrice));
        Assert.Single(services, d => d.ServiceType == typeof(IExternal));
        Assert.Equal(2, services.Count(d => d.ServiceType == typeof(IVersionFlowCatalog)));
    }

    [Fact]
    public async Task Automatic_setup_starts_successfully_with_registered_dependencies()
    {
        var builder = Host.CreateApplicationBuilder();
        foreach (var descriptor in Services()) builder.Services.Add(descriptor);
        builder.Services.AddVersionFlow();
        using var host = builder.Build(); await host.StartAsync(); await host.StopAsync();
    }

    [Fact]
    public void Explicit_assembly_selects_discovery_root_for_shared_startup_helpers()
    {
        var services = new ServiceCollection(); services.AddScoped<External1>(); services.AddScoped<External5>();
        services.AddVersionFlow(assembly: typeof(External1).Assembly);
        using var provider = services.BuildServiceProvider(); using var scope = provider.CreateScope();
        Assert.Equal("external-five", scope.ServiceProvider.GetRequiredService<IExternal>().Execute());
        Assert.Null(scope.ServiceProvider.GetService<IPrice>());
    }

    [Fact]
    public void Manual_opt_out_preserves_fluent_only_interface_ownership()
    {
        var services = RuntimeTests.Services();
        services.AddScoped<IPrice, Price1>();
        services.AddVersionFlow(o => o.Overrides.Classes["Pricing"] = new() { Version = "2" }, autoRegister: false);
        using var provider = services.BuildServiceProvider(); using var scope = provider.CreateScope();
        Assert.Equal(20m, scope.ServiceProvider.GetRequiredService<IPrice>().Calculate(2));
        Assert.Equal(18m, scope.ServiceProvider.GetRequiredService<IVersionFlow>().For<IPrice>().Resolve().Calculate(2));
    }

    [Fact]
    public void Automatic_registration_rejects_conflicting_interface_without_replacement()
    {
        var services = Services(); services.AddScoped<IPrice, Price1>();
        Assert.Throws<VersionFlowRegistrationException>(() => services.AddVersionFlow());
        Assert.Single(services, d => d.ServiceType == typeof(IPrice) && d.ImplementationType == typeof(Price1));
    }

    [Fact]
    public async Task Late_interface_conflicts_are_still_rejected_at_host_start()
    {
        var builder = Host.CreateApplicationBuilder();
        foreach (var descriptor in Services()) builder.Services.Add(descriptor);
        builder.Services.AddVersionFlow(); builder.Services.AddScoped<IPrice, Price1>();
        using var host = builder.Build();
        await Assert.ThrowsAsync<VersionFlowRegistrationException>(() => host.StartAsync());
    }
    [Fact]
    public void Additional_discovery_roots_are_linked_without_duplicate_contracts()
    {
        var services = Services();
        services.AddVersionFlow(assembly: typeof(External1).Assembly);
        services.AddVersionFlow();
        services.AddVersionFlow();
        using var provider = services.BuildServiceProvider(); using var scope = provider.CreateScope();
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IPrice>());
        Assert.Equal("external-five", scope.ServiceProvider.GetRequiredService<IExternal>().Execute());
        Assert.Equal(2, services.Count(d => d.ServiceType == typeof(IVersionFlowCatalog)));
    }
}

