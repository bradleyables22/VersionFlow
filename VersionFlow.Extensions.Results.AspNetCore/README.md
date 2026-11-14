# VersionFlow.Extensions.Results.AspNetCore

Optional .NET 10 ASP.NET Core adapter for VersionFlow.Extensions.Results. No service registration is required.

```csharp
using VersionFlow.Extensions.Results;
using VersionFlow.Extensions.Results.AspNetCore;

app.MapGet("/items", () => CursorTryResult<int>.Succeeded([]).AsHttpResult());
app.MapGet("/missing", () => TryResult<int>.Failed("Item not found.")
    .AsHttpResult(failureStatusCode: 404, failureTitle: "Not found", failureMessage: "Item not found."));
```

Commands and null scalar payloads return 204; nonnull payloads return 200. Collection/page results return 200 with the complete envelope even when empty. Failures return Problem Details with generic 500 defaults; explicitly provide safe details and a status between 400 and 599. Neither exceptions nor result messages are automatically copied into Problem Details. Applications own logging, authorization, and endpoint/OpenAPI response declarations.

See the Results package README for the contracts, JSON policy, and migration notes.

## MVC controllers and Azure Functions ASP.NET Core integration

`AsActionResult()` returns the concrete MVC base type `ActionResult`. This implements `IActionResult` and can be implicitly converted to `ActionResult<T>` in a controller return statement. It uses MVC's configured output formatters and JSON settings. It takes the same failure status/title/message arguments as `AsHttpResult()`.

```csharp
public ActionResult<string> GetItem()
    => TryResult<string>.Succeeded("example").AsActionResult();

public IActionResult GetItems()
    => CursorTryResult<int>.Succeeded([], "next-token").AsActionResult();

public ActionResult<PagedTryResult<int>> GetPage()
    => PagedTryResult<int>.Succeeded([1, 2], 1, 10, 2).AsActionResult();
```

Declare the payload type for scalar actions and the concrete envelope type for collection actions. The adapter returns MVC result objects; it does not enforce that your declared `ActionResult<T>` matches the payload, so keep the signature accurate for OpenAPI. An async action awaits its operation and then returns `result.AsActionResult()`.

Azure Functions using `ConfigureFunctionsWebApplication()` and ASP.NET Core HTTP integration can return the same `IActionResult`. The built-in isolated worker `HttpRequestData`/`HttpResponseData` model is not included in this package.

The adapters do not register MVC, configure Functions hosting, or install filters. Configure those normally in your application. MVC failures use `application/problem+json`; no result message or exception is exposed automatically.
