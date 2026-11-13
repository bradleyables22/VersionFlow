using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace VersionFlow.Tests;

public sealed class OperationValidationTests
{
    [Theory]
    [InlineData("Orders.Proces")]
    [InlineData("orders.Process")]
    [InlineData("Orders.process")]
    [InlineData("Pricing.Unknown")]
    [InlineData("Unknown.Calculate")]
    public void Unknown_operations_fail_before_service_resolution_or_callbacks(string operation)
    {
        var services = RuntimeTests.Services();
        var constructions = 0;
        var callbacks = 0;
        services.AddTransient<NewOrder>(_ => { constructions++; return new NewOrder(); });
        services.AddVersionFlow(o => o.Methods["Orders.Process"] = "1");
        using var provider = services.BuildServiceProvider(); using var scope = provider.CreateScope();
        var flow = scope.ServiceProvider.GetRequiredService<IVersionFlow>();
        var error = Assert.Throws<VersionFlowSelectionException>(() => flow.For(operation).UseLatest()
            .Match<int>().Version<NewOrder>(2, _ => { callbacks++; return 2; }).Execute());
        Assert.Contains(operation, error.Message);
        Assert.Contains("AddVersionFlowOperation", error.Message);
        Assert.Throws<VersionFlowSelectionException>(() => { flow.For(operation).UseVersion(2).MatchAsync<int>(); });
        Assert.Equal(0, constructions); Assert.Equal(0, callbacks);
    }

    [Fact]
    public async Task Declared_and_catalog_operations_remain_available()
    {
        var services = RuntimeTests.Services();
        services.AddVersionFlowOperation("Company.Orders.Process");
        services.AddVersionFlow(o => { o.Methods["Orders.Process"] = "1"; o.Methods["Company.Orders.Process"] = "2"; });
        using var provider = services.BuildServiceProvider(); using var scope = provider.CreateScope();
        var flow = scope.ServiceProvider.GetRequiredService<IVersionFlow>();
        Assert.Equal(1, flow.For("Orders.Process").Match<int>().Version<OldOrder>(1, _ => 1).Version<NewOrder>(2, _ => 2).Execute());
        Assert.Equal(2, flow.For("Company.Orders.Process").Match<int>().Version<NewOrder>(2, _ => 2).Execute());
        Assert.Equal(2, await flow.For("Pricing.Calculate").MatchAsync<int>().Version<NewOrder>(2, _ => Task.FromResult(2)).ExecuteAsync());
        Assert.Equal(3, flow.For("Ops.Calculate").Match<int>().Version<NewOrder>(3, _ => 3).Execute());
    }

    [Theory]
    [InlineData(null)] [InlineData("")] [InlineData(" ")] [InlineData("Orders")] [InlineData(".Process")] [InlineData("Orders.")]
    public void Malformed_operation_names_fail_with_argument_errors(string? operation)
    {
        var services = RuntimeTests.Services(); services.AddVersionFlowOptions();
        using var provider = services.BuildServiceProvider(); using var scope = provider.CreateScope();
        Assert.ThrowsAny<ArgumentException>(() => scope.ServiceProvider.GetRequiredService<IVersionFlow>().For(operation!));
    }
}
