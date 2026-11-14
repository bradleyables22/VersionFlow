using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using VersionFlow.TestServices;
using Xunit;
namespace VersionFlow.Tests;
public sealed class IntegrationTests
{
    [Fact] public void Linked_external_catalog_routes_and_scope_disposes_selected_service()
    {
        var services = new ServiceCollection();
        services.AddScoped<External1>(); services.AddScoped<External5>();
        services.AddVersionFlowCatalog<Generated.Assembly_VersionFlow_TestServices.GeneratedCatalog>();
        services.AddVersionFlow(o => o.Overrides.Classes["External"] = new() { Version = "1" }); services.AddVersionFlowDispatchers();
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        External1 instance;
        using (var scope = provider.CreateScope())
        {
            var flow = scope.ServiceProvider.GetRequiredService<IVersionFlow>();
            Assert.Equal("external-one", scope.ServiceProvider.GetRequiredService<IExternal>().Execute());
            Assert.Equal("external-five", flow.For<IExternal>().UseLatest().Resolve().Execute());
            instance = scope.ServiceProvider.GetRequiredService<External1>();
            Assert.False(instance.Disposed);
        }
        Assert.True(instance.Disposed);
    }
    [Fact] public async Task ValidateOnStart_false_defers_validation_until_first_use()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddVersionFlow(o => { o.ValidateOnStart = false; o.Default = "invalid"; });
        using var host = builder.Build(); await host.StartAsync();
        Assert.Throws<VersionFlowConfigurationException>(() => host.Services.GetRequiredService<VersionFlowSelector>());
        await host.StopAsync();
    }
    [Fact] public async Task Options_builder_explicit_startup_validation_is_honored()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddVersionFlowOptions().Configure(o => { o.ValidateOnStart = false; o.Default = "invalid"; }).ValidateOnStart();
        using var host = builder.Build();
        await Assert.ThrowsAsync<OptionsValidationException>(() => host.StartAsync());
    }
    [Fact] public void Invalid_reload_with_external_options_monitor_does_not_replace_runtime_snapshot()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Overrides:Classes:Pricing:Version"] = "1" }).Build();
        var services = RuntimeTests.Services(); services.AddVersionFlow(configuration);
        using var provider = services.BuildServiceProvider();
        var selector = provider.GetRequiredService<VersionFlowSelector>();
        var monitor = provider.GetRequiredService<IOptionsMonitor<VersionFlowOptions>>();
        Assert.Equal("1", monitor.CurrentValue.Overrides.Classes["Pricing"].Version);
        configuration["Overrides:Classes:Pricing:Version"] = "9";
        // Microsoft options consumers retain their native validation behavior.
        Assert.ThrowsAny<Exception>(() => configuration.Reload());
        Assert.Equal(1, selector.Select("Pricing", "Calculate").Number);
        configuration["Overrides:Classes:Pricing:Version"] = "2"; configuration.Reload();
        Assert.Equal(2, selector.Select("Pricing", "Calculate").Number);
        Assert.Equal("2", monitor.CurrentValue.Overrides.Classes["Pricing"].Version);
    }
    [Fact] public void Json_numeric_versions_bind_and_case_sensitive_keys_are_validated()
    {
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("""{"Overrides":{"Classes":{"Pricing":{"Version":1}}}}"""));
        var configuration = new ConfigurationBuilder().AddJsonStream(stream).Build();
        var services = RuntimeTests.Services(); services.AddVersionFlow(configuration);
        using var provider = services.BuildServiceProvider();
        Assert.Equal(1, provider.GetRequiredService<VersionFlowSelector>().Select("Pricing").Number);
        var other = RuntimeTests.Services(); other.AddVersionFlow(o => o.Overrides.Classes["pricing"] = new() { Version = "1" });
        using var invalid = other.BuildServiceProvider();
        Assert.Throws<VersionFlowConfigurationException>(() => invalid.GetRequiredService<VersionFlowSelector>());
    }
    [Fact] public void Postconfigure_and_custom_validators_participate_in_reload()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();
        var services = RuntimeTests.Services(); services.AddVersionFlowOptions().Bind(configuration).Validate(o => o.Default != "2", "policy");
        services.PostConfigure<VersionFlowOptions>(o => o.Overrides.Classes["Ops"] = new() { Version = "Latest" });
        using var provider = services.BuildServiceProvider();
        var selector = provider.GetRequiredService<VersionFlowSelector>();
        Assert.Equal(2, selector.Select("Pricing").Number);
        var identity = selector.Select("Pricing").SnapshotIdentity;
        configuration["Default"] = "2"; configuration.Reload();
        Assert.Equal(identity, selector.Select("Pricing").SnapshotIdentity);
    }
    [Fact] public void User_constructor_exceptions_propagate_unchanged()
    {
        var expected = new InvalidOperationException("Constructor failed");
        var services = RuntimeTests.Services();
        services.AddScoped<NewOrder>(_ => throw expected); services.AddVersionFlowOptions();
        using var provider = services.BuildServiceProvider(); using var scope = provider.CreateScope();
        var flow = scope.ServiceProvider.GetRequiredService<IVersionFlow>();
        Assert.Same(expected, Assert.Throws<InvalidOperationException>(() => flow.For("Orders.Process").Match<string>().Version<NewOrder>(2, _ => "never").Execute()));
    }
    [Fact] public void Unknown_contract_and_duplicate_linked_catalogs_fail_clearly()
    {
        var services = new ServiceCollection(); services.AddVersionFlowOptions(autoRegister: false);
        using var provider = services.BuildServiceProvider(); using var scope = provider.CreateScope();
        Assert.Throws<VersionFlowRegistrationException>(() => scope.ServiceProvider.GetRequiredService<IVersionFlow>().For<IExternal>());
        Assert.Throws<VersionFlowConfigurationException>(() => new VersionFlowCatalog([new Generated.Assembly_VersionFlow_TestServices.GeneratedCatalog(), new Generated.Assembly_VersionFlow_TestServices.GeneratedCatalog()], []));
    }
}

