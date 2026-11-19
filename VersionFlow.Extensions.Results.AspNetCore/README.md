# VersionFlow.Extensions.Results.AspNetCore

Optional .NET ASP.NET Core adapter for VersionFlow.Extensions.Results. No service registration is required.

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

## Reading HTTP responses

The inbound extensions read `HttpResponseMessage` instances obtained from `HttpClient`:

```csharp
using var response = await client.GetAsync("/items/123", cancellationToken);
TryResult<Item> result = await response.FromHttpResponseAsync<Item>(cancellationToken: cancellationToken);
```

Use `FromHttpResponseAsync()` for commands, and the generic overload for a raw JSON payload. HTTP 204 maps to successful `default(T)`; choose a nullable payload type when absence matters. Other successful statuses require valid JSON for typed results.

Collection endpoints return envelopes. Read them with `FromHttpCollectionAsync<T>()`, `FromHttpPageAsync<T>()`, `FromHttpOffsetAsync<T>()`, or `FromHttpCursorAsync<T>()`. These preserve metadata and validate it through the result factories. Missing envelopes or required paging metadata produce failures, including HTTP 204 for collection readers. JSON options may be supplied to typed readers; defaults follow System.Text.Json web conventions.

Non-success HTTP statuses produce failed results. For `application/problem+json`, the message uses nonblank `detail`, then `title`; otherwise it uses the numeric HTTP status. Remote Problem Details text is used as supplied. Arbitrary error bodies are not copied into messages. Invalid JSON and response read errors become failures with diagnostic exceptions; cancellation propagates. Network errors thrown by `GetAsync` occur before these converters run. The caller retains ownership of the response and must dispose it.
