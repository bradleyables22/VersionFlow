# VersionFlow.Extensions.Results

Standalone .NET result contracts. No Core, DI, Railway, or ASP.NET dependency.

```csharp
var saved = TryResult.Succeeded();
var item = TryResult<string>.Succeeded("example");
var failure = TryResult<string>.Failed("Unable to load the item.", exception);
var collection = CollectionTryResult<int>.Succeeded([1, 2]);
var page = PagedTryResult<int>.Succeeded([3, 4], pageNumber: 2, pageSize: 2, totalCount: 5);
var offset = OffsetTryResult<int>.Succeeded([3, 4], offset: 2, limit: 2, totalCount: 5);
var cursor = CursorTryResult<int>.Succeeded([3, 4], nextCursor: "opaque-token");
```

## Design decisions

`Succeeded` / `Failed` describe the outcome being created; `Pass` / `Fail` sound like operations or validation instructions. `Success` stays the boolean discriminator. There are no aliases or implicit conversions. Expected failures need only a nonblank caller-safe message, not an exception. Exceptions are optional diagnostic context and are never automatically copied into messages.

Results have private constructors and get-only properties so callers cannot create contradictory states. `TryResult<T>` permits a successful null; check `Success`, since a failed value type has `default(T)` in `Data`. Collections require a nonnull sequence and snapshot it into a read-only list; contained objects are not deep-cloned. Empty success uses `[]`. Failures have empty collections and null paging metadata.

`PagedTryResult<T>` uses one-based page numbers, a positive page size, and an exact nonnegative total. `OffsetTryResult<T>` uses a zero-based offset and positive limit. Both validate that returned items fit the requested window and remaining total; an empty page beyond the end is allowed. Total/page arithmetic uses `long`. Offset continuation advances by the actual returned count; an empty offset page cannot advance.

`CursorTryResult<T>` uses a nullable opaque string, not `object`. Encode database-specific or composite cursors at the application boundary. A null token means completion; an empty string is rejected. An empty page may still carry a token (for example, filtered upstream pages). Cursor paging deliberately requires no total count. Ordinary lists can also use `TryResult<IReadOnlyList<T>>`; the collection variant adds a stable envelope and count. Use Railway when you need validation, recovery, and operation composition rather than duplicating those APIs here.

## JSON and HTTP

With System.Text.Json, `Success`, `Message`, data, and paging metadata serialize. Only `Exception` is ignored. Hiding the discriminator makes an independently serialized failure indistinguishable from an empty response. Messages therefore must be safe for callers. No Newtonsoft dependency is included; applications using a different serializer must configure diagnostic exclusion themselves.

These are producer-side outcomes, not deserializable request/transport DTOs. Deserialize API responses into application-owned DTOs. Serialize concrete result types (or `object`) to retain paging metadata; the common abstract bases are not polymorphic wire contracts. Naming and null omission follow the application's JSON options.

The optional `VersionFlow.Extensions.Results.AspNetCore` package supplies `AsHttpResult`: payloads return 200, successful commands/null values return 204, and collection/page envelopes always return 200 even when empty. Failures default to generic 500 Problem Details. Callers explicitly select a 4xx/5xx code and safe detail; exception text and result messages are not automatically exposed by this adapter. No automatic logging claim or catch-all suppression is included.

## Timestamp cursors

```csharp
string token = CursorTime.FormatUtc(DateTimeOffset.UtcNow);
bool valid = CursorTime.TryParse(token, out DateTimeOffset utc);
```

The canonical format is UTC `yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'`. Parsing requires exactly seven fractional digits and `Z`. Offset-bearing values are normalized to UTC; unspecified `DateTime` values are rejected rather than interpreted using the server timezone. `CursorTimeJsonConverter` is an opt-in System.Text.Json converter for `DateTimeOffset` cursor properties; attach it with `[JsonConverter(typeof(CursorTimeJsonConverter))]`. Do not register it globally unless all dates use this contract. Timestamp-only cursors require a unique ordering; use a composite timestamp/ID token when timestamps can tie. Tokens are encoding, not authentication.

The old Daas `OffSetResponse` example maps to `CursorTryResult<T>`, not offset pagination. Replace `Pass(null, null)` with `Succeeded([])`, and format numeric/time cursors explicitly as strings. Repository-specific response conversion belongs in the consuming application.
