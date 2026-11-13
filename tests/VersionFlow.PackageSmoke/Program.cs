using PackageLibrary;
using Microsoft.Extensions.DependencyInjection;
using VersionFlow;

var services = new ServiceCollection();
services.AddScoped<ExternalOperation1>(); services.AddScoped<ExternalOperation4>();
services.AddScoped<Price1>(); services.AddScoped<Price2>(); services.AddScoped<Operations>();
services.AddVersionFlowOperation("Orders.Process");
services.AddVersionFlow(o => { o.Classes["Pricing"] = "1"; o.Methods["Pricing.Calculate"] = "2"; o.Methods["Orders.Process"] = "1"; });
using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
using var scope = provider.CreateScope();
Check(scope.ServiceProvider.GetRequiredService<IExternalOperation>().Read() == 4, "referenced library automatic discovery");
var flow = scope.ServiceProvider.GetRequiredService<IVersionFlow>();
var price = scope.ServiceProvider.GetRequiredService<IPrice>();
Check(price.Calculate() == 2 && price.Discount() == 1, "method/class precedence");
Check(flow.For<IPrice>().UseVersion(1).Resolve().Calculate() == 1, "explicit override");
Check(flow.For<IPrice>().UseLatest().Resolve().Discount() == 2, "latest override");
Check(scope.ServiceProvider.GetRequiredService<IOperations>().Run() == 3, "independent method dispatch");
Check(await flow.For("Orders.Process").MatchAsync<int>().Version<Price1>(1, p => Task.FromResult(p.Calculate())).ExecuteAsync() == 1, "fluent execution");
try { flow.For("Orders.Proces"); throw new Exception("Unknown operation was accepted"); }
catch (VersionFlowSelectionException) { }
Console.WriteLine("Core package smoke test passed: bundled generator, dispatchers, precedence, fluent API, and unknown-operation rejection.");
static void Check(bool condition, string scenario) { if (!condition) throw new Exception("Failed: " + scenario); }

public interface IPrice { int Calculate(); int Discount(); }
[Versioned(typeof(IPrice), 1, Key = "Pricing")]
public sealed class Price1 : IPrice { public int Calculate() => 1; public int Discount() => 1; }
[Versioned(typeof(IPrice), 2, Key = "Pricing")]
public sealed class Price2 : IPrice { public int Calculate() => 2; public int Discount() => 2; }
public interface IOperations { int Run(); }
[Versioned(typeof(IOperations))]
public sealed class Operations { [Versioned(nameof(IOperations.Run), 3)] public int Third() => 3; }


