using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace VersionFlow.Tests;

public sealed class FeatureFlagTests
{
    [Fact]
    public void Flags_work_without_versioned_services_and_use_ordinal_names()
    {
        var flags = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase)
        {
            ["EnableExports"] = true,
            ["NewCheckout"] = false
        };
        var services = new ServiceCollection();
        services.AddVersionFlow(o => o.Flags = flags, autoRegister: false);
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using var scope = provider.CreateScope();
        var flow = scope.ServiceProvider.GetRequiredService<IVersionFlow>();
        Assert.True(flow.FlagIsEnabled("EnableExports"));
        Assert.False(flow.FlagIsDisabled("EnableExports"));
        Assert.False(flow.FlagIsEnabled("enableexports"));
        Assert.True(flow.FlagIsDisabled("enableexports"));
        Assert.False(flow.FlagIsEnabled("NewCheckout"));
        Assert.True(flow.FlagIsDisabled("NewCheckout"));
        Assert.False(flow.FlagIsEnabled("Missing"));
        Assert.True(flow.FlagIsDisabled("Missing"));
        Assert.Throws<ArgumentNullException>(() => flow.FlagIsEnabled(null!));
        Assert.Throws<ArgumentNullException>(() => flow.FlagIsDisabled(null!));
        flags["EnableExports"] = false;
        Assert.True(flow.FlagIsEnabled("EnableExports"));
        Assert.False(flow.FlagIsDisabled("EnableExports"));
    }

    [Fact]
    public void Bound_flags_reload_in_existing_scope_and_invalid_updates_retain_entire_snapshot()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["VersionFlow:Flags:EnableExports"] = "true",
            ["VersionFlow:Overrides:Classes:Pricing:Version"] = "1"
        }).Build();
        var services = RuntimeTests.Services();
        services.AddVersionFlow(configuration.GetSection("VersionFlow"));
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var flow = scope.ServiceProvider.GetRequiredService<IVersionFlow>();
        Assert.True(flow.FlagIsEnabled("EnableExports"));
        Assert.False(flow.FlagIsDisabled("EnableExports"));
        Assert.False(flow.FlagIsEnabled("enableexports"));
        Assert.True(flow.FlagIsDisabled("enableexports"));

        configuration["VersionFlow:Flags:EnableExports"] = "false";
        configuration["VersionFlow:Overrides:Classes:Pricing:Version"] = "9";
        configuration.Reload();
        Assert.True(flow.FlagIsEnabled("EnableExports"));
        Assert.False(flow.FlagIsDisabled("EnableExports"));
        Assert.Equal(1, flow.Select("Pricing").Number);

        configuration["VersionFlow:Overrides:Classes:Pricing:Version"] = "2";
        configuration.Reload();
        Assert.False(flow.FlagIsEnabled("EnableExports"));
        Assert.True(flow.FlagIsDisabled("EnableExports"));
        Assert.Equal(2, flow.Select("Pricing").Number);

        configuration["VersionFlow:Flags:EnableExports"] = "not-a-bool";
        configuration.Reload();
        Assert.False(flow.FlagIsEnabled("EnableExports"));
        Assert.True(flow.FlagIsDisabled("EnableExports"));

        configuration["VersionFlow:Flags:EnableExports"] = "true";
        configuration.Reload();
        Assert.True(flow.FlagIsEnabled("EnableExports"));
        Assert.False(flow.FlagIsDisabled("EnableExports"));
        configuration.Providers.Single().Set("VersionFlow:Flags:EnableExports", null);
        configuration.Reload();
        Assert.False(flow.FlagIsEnabled("EnableExports"));
        Assert.True(flow.FlagIsDisabled("EnableExports"));
    }

    [Fact]
    public void Null_flags_are_rejected_as_invalid_configuration()
    {
        var services = new ServiceCollection();
        services.AddVersionFlow(o => o.Flags = null!, autoRegister: false);
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var error = Assert.Throws<VersionFlowConfigurationException>(() => scope.ServiceProvider.GetRequiredService<IVersionFlow>());
        Assert.Contains("Flags cannot be null", error.Message);
    }
}
