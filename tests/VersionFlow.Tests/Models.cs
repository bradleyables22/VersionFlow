using VersionFlow;
namespace VersionFlow.Tests;

public interface IBasePrice { Guid Identity(); }
public interface IPrice : IBasePrice
{
    decimal Calculate(int quantity);
    decimal Discount(decimal amount);
    void Touch();
    Task<string?> EchoAsync(string? value, CancellationToken cancellationToken);
}
public sealed class FirstDependency { public int Calls; }
public sealed class SecondDependency { public int Calls; }
[Versioned(typeof(IPrice), 1, Key = "Pricing")]
public sealed class Price1(FirstDependency dependency) : IPrice
{
    private readonly Guid identity = Guid.NewGuid();
    public Guid Identity() => identity;
    public decimal Calculate(int quantity) { dependency.Calls++; return quantity * 10m; }
    public decimal Discount(decimal amount) => amount * 0.95m;
    public void Touch() => dependency.Calls++;
    public Task<string?> EchoAsync(string? value, CancellationToken cancellationToken) => Task.FromResult(value);
}
[Versioned(typeof(IPrice), 2, Key = "Pricing")]
public sealed class Price2(SecondDependency dependency) : IPrice
{
    private readonly Guid identity = Guid.NewGuid();
    public Guid Identity() => identity;
    decimal IPrice.Calculate(int quantity) { dependency.Calls++; return quantity * 9m; }
    public decimal Discount(decimal amount) => amount * 0.9m;
    public void Touch() => dependency.Calls++;
    public Task<string?> EchoAsync(string? value, CancellationToken cancellationToken) => Task.FromResult(value);
}
public interface IOperations { int Calculate(int value); int Discount(int value); }
[Versioned(typeof(IOperations), Key = "Ops")]
public sealed class Operations(FirstDependency dependency)
{
    [Versioned("Calculate", 1)] public int One(int value) { dependency.Calls++; return value; }
    [Versioned("Calculate", 3)] public int Three(int value) => value * 3;
    [Versioned("Discount", 1)] public int Discount(int value) => value - 1;
}
public sealed class OldOrder { public string Process(int id) => "old:" + id; }
public sealed class NewOrder
{
    public string Process(int id, bool validate) => $"new:{id}:{validate}";
    public Task<string> ProcessAsync(int id, bool validate, CancellationToken token) { token.ThrowIfCancellationRequested(); return Task.FromResult(Process(id, validate)); }
}

