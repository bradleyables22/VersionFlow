using VersionFlow.Extensions.DI;
using VersionFlow.Extensions.Cqrs;
using VersionFlow;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddAttributedServices();

builder.Services.AddVersionFlowOperation("Orders.Process");
builder.Services.AddVersionFlow(builder.Configuration.GetSection("VersionFlow"));
var app = builder.Build();
app.MapGet("/", (IPricingService pricing, IPricingOperations operations) => new
{
    Total = pricing.Calculate(10), Discounted = pricing.Discount(100),
    OperationsTotal = operations.Calculate(10), OperationsDiscounted = operations.Discount(100)
});
app.MapGet("/explicit/{version:int}", (int version, IVersionFlow flow) => flow.For<IPricingService>().UseVersion(version).Resolve().Calculate(10));
app.MapGet("/orders/{id:int}", async (int id, IVersionFlow flow, CancellationToken ct) => await flow.For("Orders.Process").MatchAsync<string>()
    .Version<OrderV1>(1, service => service.Process(id))
    .Version<OrderV2>(2, (service, cancellation) => service.Process(id, true, cancellation)).ExecuteAsync(ct));
app.MapGet("/cqrs/{quantity:int}", (int quantity, ICqrsHandler<CalculatePrice, decimal> handler, CancellationToken ct) => handler.HandleAsync(new CalculatePrice(quantity), ct));
app.Run();

public interface IPricingService { decimal Calculate(int quantity); decimal Discount(decimal amount); }
[Versioned(typeof(IPricingService), 1, Key = "Pricing")]
[Service]
public sealed class PricingV1(ILogger<PricingV1> logger) : IPricingService
{
    public decimal Calculate(int quantity) { logger.LogDebug("Pricing v1"); return quantity * 10m; }
    public decimal Discount(decimal amount) => amount * 0.95m;
}
[Versioned(typeof(IPricingService), 2, Key = "Pricing")]
[Service]
public sealed class PricingV2(TimeProvider clock) : IPricingService
{
    public decimal Calculate(int quantity) => quantity * (clock.GetUtcNow().Year > 2000 ? 9.5m : 10m);
    public decimal Discount(decimal amount) => amount * 0.90m;
}
public interface IPricingOperations { decimal Calculate(int quantity); decimal Discount(decimal amount); }
[Versioned(typeof(IPricingOperations), Key = "PricingOps")]
[Service]
public sealed class PricingOperations(ILogger<PricingOperations> logger)
{
    [Versioned("Calculate", 1)] public decimal CalculateV1(int quantity) => quantity * 10m;
    [Versioned("Calculate", 2)] public decimal CalculateV2(int quantity) { logger.LogDebug("Operations v2"); return quantity * 9.5m; }
    [Versioned("Discount", 1)] public decimal DiscountV1(decimal amount) => amount * 0.95m;
}
[Service]
public sealed class OrderV1 { public Task<string> Process(int id) => Task.FromResult($"Order {id}: v1"); }
[Service]
public sealed class OrderV2
{
    public Task<string> Process(int id, bool validate, CancellationToken ct)
    { ct.ThrowIfCancellationRequested(); return Task.FromResult($"Order {id}: v2, validate={validate}"); }
}

public sealed record CalculatePrice(int Quantity);
[Service(typeof(ICqrsHandler<CalculatePrice, decimal>))]
public sealed class CalculatePriceHandler(IPricingService pricing) : ICqrsHandler<CalculatePrice, decimal>
{
    public Task<decimal> HandleAsync(CalculatePrice request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(pricing.Calculate(request.Quantity));
    }
}



